using CommunityToolkit.Mvvm.Input;
using ICSharpCode.SharpZipLib.Zip;
//#define ENABLE_ROSLYN

using CommunityToolkit.Mvvm.Input;
using ICSharpCode.SharpZipLib.Zip;
#if ENABLE_ROSLYN
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
#endif
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

namespace Voidstrap.UI.ViewModels.Settings
{
    public class PluginsViewModel : INotifyPropertyChanged
    {
        private bool _suppressCodeSync = false;
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        #region Collections & Selection
        public ObservableCollection<PluginModel> LoadedPlugins { get; } = new();
        public ObservableCollection<PluginModel> PublicPlugins { get; } = new();

        private PluginModel _selectedPlugin;
        public PluginModel SelectedPlugin
        {
            get => _selectedPlugin;
            set
            {
                if (SetProperty(ref _selectedPlugin, value))
                    RunPluginCommand.NotifyCanExecuteChanged();
                    SavePlayArea();
            }
        }

        private PluginModel _selectedPublicPlugin;
        public PluginModel SelectedPublicPlugin
        {
            get => _selectedPublicPlugin;
            set
            {
                if (SetProperty(ref _selectedPublicPlugin, value))
                    LoadPublicPluginCommand.NotifyCanExecuteChanged();
            }
        }
        #endregion

        #region Plugin Code & Preview
        private string _pluginXamlCode;
        public string PluginXamlCode
        {
            get => _pluginXamlCode;
            set
            {
                if (SetProperty(ref _pluginXamlCode, value))
                {


                    UpdateLivePreview();
                    AutoSavePlugin();
                }
            }
        }

        private string _pluginCsCode;
        public string PluginCsCode
        {
            get => _pluginCsCode;
            set
            {
                if (SetProperty(ref _pluginCsCode, value))
                {
                    AutoFixPluginCode();
                    UpdateLivePreview();
                    AutoSavePlugin();
                }
            }
        }

        private FrameworkElement _pluginPreview;
        public FrameworkElement PluginPreview
        {
            get => _pluginPreview;
            set => SetProperty(ref _pluginPreview, value);
        }

        private string _newPluginName;
        public string NewPluginName
        {
            get => _newPluginName;
            set
            {
                if (SetProperty(ref _newPluginName, value))
                    AddPluginCommand.NotifyCanExecuteChanged();
            }
        }
        #endregion

        #region Commands
        public RelayCommand CompileAndLoadPluginCommand { get; }
        public RelayCommand SavePluginCommand { get; }
        public RelayCommand NewPluginCommand { get; }
        public RelayCommand LoadPublicPluginCommand { get; }
        public RelayCommand RefreshPublicPluginsCommand { get; }
        public RelayCommand RunPluginCommand { get; }
        public RelayCommand AddPluginCommand { get; }
        #endregion

        #region AutoSave Path
        private readonly string AutoSavePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Voidstrap",
            "autosave_plugin.zip"
        );
        #endregion

        public PluginsViewModel()
        {
            CompileAndLoadPluginCommand = new RelayCommand(CompileAndLoadPlugin);
            SavePluginCommand = new RelayCommand(SavePlugin);
            NewPluginCommand = new RelayCommand(NewPlugin);
            LoadPublicPluginCommand = new RelayCommand(LoadPublicPlugin, () => SelectedPublicPlugin != null);
            RefreshPublicPluginsCommand = new RelayCommand(RefreshPublicPlugins);
            RunPluginCommand = new RelayCommand(RunSelectedPlugin, CanRunPlugin);
            AddPluginCommand = new RelayCommand(AddPluginByName, CanAddPlugin);
            LoadedPlugins.CollectionChanged += (_, __) => SavePlayArea();
            AutoLoadPlugin();
            LoadPlayArea();

            if (string.IsNullOrWhiteSpace(PluginXamlCode))
                NewPlugin();
        }

        #region Live Preview
        private void UpdateLivePreview()
        {
            if (string.IsNullOrWhiteSpace(PluginXamlCode))
            {
                PluginPreview = null;
                return;
            }

            try
            {
                var parsed = XamlReader.Parse(PluginXamlCode);

                if (parsed is Window w)
                    PluginPreview = new UserControl { Content = w.Content };
                else if (parsed is FrameworkElement fe)
                    PluginPreview = new UserControl { Content = fe };
                else
                    PluginPreview = null;
            }
            catch
            {
                PluginPreview = null;
            }
        }
        #endregion

        #region Auto Fix & Conversion
        private void AutoFixPluginCode()
        {
            if (string.IsNullOrWhiteSpace(PluginCsCode)) return;

            if (!PluginCsCode.Contains("using System;"))
                PluginCsCode = "using System;\n" + PluginCsCode;
            if (!PluginCsCode.Contains("using System.Windows;"))
                PluginCsCode = "using System.Windows;\n" + PluginCsCode;
        }

        private void SavePlayArea()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(PluginSessionPath));

                var session = new
                {
                    Plugins = LoadedPlugins.Select(p => new
                    {
                        p.Name,
                        p.Author,
                        p.Description,
                        p.PluginXaml
                    }).ToList(),
                    SelectedPlugin = SelectedPlugin?.Name
                };

                var json = System.Text.Json.JsonSerializer.Serialize(session, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(PluginSessionPath, json);
            }
            catch (Exception ex)
            {
                Frontend.ShowMessageBox($"Failed to save play area: {ex.Message}");
            }
        }

        private readonly string PluginSessionPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Voidstrap",
            "plugin_session.json"
        );

        private void LoadPlayArea()
        {
            try
            {
                if (!File.Exists(PluginSessionPath))
                    return;

                var json = File.ReadAllText(PluginSessionPath);
                var session = System.Text.Json.JsonSerializer.Deserialize<PlayAreaSession>(json);
                if (session?.Plugins != null)
                {
                    LoadedPlugins.Clear();
                    foreach (var p in session.Plugins)
                    {
                        LoadedPlugins.Add(new PluginModel
                        {
                            Name = p.Name,
                            Author = p.Author,
                            Description = p.Description,
                            PluginXaml = p.PluginXaml
                        });
                    }

                    if (!string.IsNullOrEmpty(session.SelectedPlugin))
                    {
                        SelectedPlugin = LoadedPlugins.FirstOrDefault(p => p.Name == session.SelectedPlugin);
                        if (SelectedPlugin != null)
                            PluginXamlCode = SelectedPlugin.PluginXaml;
                    }
                }
            }
            catch (Exception ex)
            {
                Frontend.ShowMessageBox($"Failed to load play area: {ex.Message}");
            }
        }

        private class PlayAreaSession
        {
            public List<PluginModel> Plugins { get; set; }
            public string SelectedPlugin { get; set; }
        }

        private void ConvertXamlToCSharp()
        {
            if (string.IsNullOrWhiteSpace(PluginXamlCode))
                return;

            try
            {
                var className = "GeneratedPlugin";
                var sb = new StringBuilder();
                sb.AppendLine("using System;");
                sb.AppendLine("using System.Collections.Generic;");
                sb.AppendLine("using System.Windows;");
                sb.AppendLine("using System.Windows.Controls;");
                sb.AppendLine("using System.Windows.Markup;");
                sb.AppendLine("using System.Windows.Media;");
                sb.AppendLine();
                sb.AppendLine($"public class {className} : Window");
                sb.AppendLine("{");
                sb.AppendLine($"    public {className}()");
                sb.AppendLine("    {");
                sb.AppendLine("        InitializeComponent();");
                sb.AppendLine("    }");
                sb.AppendLine();
                sb.AppendLine("    private void InitializeComponent()");
                sb.AppendLine("    {");
                sb.AppendLine("        var xaml = @\"" + PluginXamlCode.Replace("\"", "\"\"") + "\";");
                sb.AppendLine("        var parsed = (Window)XamlReader.Parse(xaml);");
                sb.AppendLine("        this.Content = parsed.Content;");
                sb.AppendLine("        this.Title = parsed.Title;");
                sb.AppendLine("        this.Width = parsed.Width;");
                sb.AppendLine("        this.Height = parsed.Height;");
                sb.AppendLine("        this.Loaded += (s, e) => AttachButtonHandlers(this);");
                sb.AppendLine("    }");
                sb.AppendLine();
                sb.AppendLine("    private void AttachButtonHandlers(DependencyObject root)");
                sb.AppendLine("    {");
                sb.AppendLine("        foreach (var btn in FindVisualChildren<Button>(root))");
                sb.AppendLine("        {");
                sb.AppendLine("            btn.Click -= Button_Click;");
                sb.AppendLine("            btn.Click += Button_Click;");
                sb.AppendLine("        }");
                sb.AppendLine("    }");
                sb.AppendLine();
                sb.AppendLine("    private void Button_Click(object sender, RoutedEventArgs e)");
                sb.AppendLine("    {");
                sb.AppendLine("        if (sender is Button b)");
                sb.AppendLine("            MessageBox.Show($\"{b.Content} clicked!\", \"Plugin\", MessageBoxButton.OK);");
                sb.AppendLine("    }");
                sb.AppendLine();
                sb.AppendLine("    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject");
                sb.AppendLine("    {");
                sb.AppendLine("        if (parent == null) yield break;");
                sb.AppendLine("        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)");
                sb.AppendLine("        {");
                sb.AppendLine("            var child = VisualTreeHelper.GetChild(parent, i);");
                sb.AppendLine("            if (child is T t) yield return t;");
                sb.AppendLine("            foreach (var sub in FindVisualChildren<T>(child)) yield return sub;");
                sb.AppendLine("        }");
                sb.AppendLine("    }");
                sb.AppendLine("}");

                PluginCsCode = sb.ToString();
            }
            catch (Exception ex)
            {
                Frontend.ShowMessageBox($"Error generating C#: {ex.Message}");
            }
        }
        #endregion

        #region Compilation
        private void CompileAndLoadPlugin()
        {
            try
            {
#if ENABLE_ROSLYN
                var assembly = CompileCSharp(PluginCsCode);
                if (assembly == null) return;

                var type = assembly.GetTypes().FirstOrDefault(t => typeof(Window).IsAssignableFrom(t));
                if (type == null)
                {
                    Frontend.ShowMessageBox("No Window-derived plugin class found.");
                    return;
                }

                var instance = Activator.CreateInstance(type) as Window;
                if (instance == null)
                {
                    Frontend.ShowMessageBox("Failed to create plugin instance.");
                    return;
                }

                var plugin = new PluginModel
                {
                    Name = string.IsNullOrWhiteSpace(NewPluginName) ? "New Plugin" : NewPluginName,
                    Instance = instance,
                    PluginXaml = PluginXamlCode
                };

                LoadedPlugins.Add(plugin);
                SelectedPlugin = plugin;
#else
                Frontend.ShowMessageBox("Plugin compilation is currently disabled to reduce application size.");
#endif
            }
            catch (Exception ex)
            {
                Frontend.ShowMessageBox($"Error loading plugin: {ex.Message}");
            }
        }

#if ENABLE_ROSLYN
        private Assembly CompileCSharp(string code)
        {
            try
            {
                var syntaxTree = CSharpSyntaxTree.ParseText(code);
                string assemblyName = Path.GetRandomFileName();

                var refs = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                    .Select(a => MetadataReference.CreateFromFile(a.Location));

                var compilation = CSharpCompilation.Create(
                    assemblyName,
                    new[] { syntaxTree },
                    refs,
                    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

                using var ms = new MemoryStream();
                var result = compilation.Emit(ms);

                if (!result.Success)
                {
                    var errors = string.Join("\n", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
                    Frontend.ShowMessageBox("Compilation failed:\n" + errors);
                    return null;
                }

                ms.Seek(0, SeekOrigin.Begin);
                return Assembly.Load(ms.ToArray());
            }
            catch (Exception ex)
            {
                Frontend.ShowMessageBox("Compilation exception: " + ex.Message);
                return null;
            }
        }
#endif

        #endregion

        #region Plugin Save / Load
        private void SavePlugin()
        {
            try
            {
                AutoSavePlugin();
                Frontend.ShowMessageBox("Plugin saved successfully!");
            }
            catch (Exception ex)
            {
                Frontend.ShowMessageBox($"Save failed: {ex.Message}");
            }
        }

        private void AutoSavePlugin()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(AutoSavePath));
                using var fs = File.Create(AutoSavePath);
                using var zip = new ICSharpCode.SharpZipLib.Zip.ZipOutputStream(fs);

                var xamlBytes = Encoding.UTF8.GetBytes(PluginXamlCode ?? "");
                var csBytes = Encoding.UTF8.GetBytes(PluginCsCode ?? "");

                zip.PutNextEntry(new ICSharpCode.SharpZipLib.Zip.ZipEntry("Plugin.xaml"));
                zip.Write(xamlBytes, 0, xamlBytes.Length);

                zip.PutNextEntry(new ICSharpCode.SharpZipLib.Zip.ZipEntry("Plugin.cs"));
                zip.Write(csBytes, 0, csBytes.Length);

                zip.Finish();
                SavePlayArea();
            }
            catch (Exception ex)
            {
                Frontend.ShowMessageBox($"Auto-save failed: {ex.Message}");
            }
        }


        private void AutoLoadPlugin()
        {
            try
            {
                if (!File.Exists(AutoSavePath))
                    return;

                using var fs = File.OpenRead(AutoSavePath);
                using var zip = new ZipInputStream(fs);
                ZipEntry entry;
                string xaml = null, cs = null;

                while ((entry = zip.GetNextEntry()) != null)
                {
                    using var ms = new MemoryStream();
                    zip.CopyTo(ms);
                    var data = Encoding.UTF8.GetString(ms.ToArray());

                    if (entry.Name.EndsWith(".xaml"))
                        xaml = data;
                    else if (entry.Name.EndsWith(".cs"))
                        cs = data;
                }

                if (!string.IsNullOrEmpty(xaml))
                    PluginXamlCode = xaml;
                if (!string.IsNullOrEmpty(cs))
                    PluginCsCode = cs;
            }
            catch (Exception ex)
            {
                Frontend.ShowMessageBox($"Auto-load failed: {ex.Message}");
            }
        }
        #endregion

        #region Plugin Management
        private void NewPlugin()
        {
            _suppressCodeSync = false;
            NewPluginName = $"NewPlugin_{DateTime.Now:MMddHHmm}";
            PluginXamlCode = @"
<Window xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
        xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
        Title=""New Plugin"" Width=""300"" Height=""200"">
    <StackPanel VerticalAlignment=""Center"" HorizontalAlignment=""Center"">
        <TextBlock Text=""Hello, World!"" FontSize=""16"" HorizontalAlignment=""Center""/>
        <Button Content=""Click Me!"" Margin=""5""/>
    </StackPanel>
</Window>";
            ConvertXamlToCSharp();
            UpdateLivePreview();
            AutoSavePlugin();
            SavePlayArea();
            Frontend.ShowMessageBox("New plugin template created!");
        }

        private void LoadPublicPlugin()
        {
            if (SelectedPublicPlugin == null) return;
            LoadedPlugins.Add(new PluginModel
            {
                Name = SelectedPublicPlugin.Name,
                Author = SelectedPublicPlugin.Author,
                Description = SelectedPublicPlugin.Description
            });
        }

        private void RefreshPublicPlugins() => Frontend.ShowMessageBox("Public plugin list refreshed!");
        private void RunSelectedPlugin() => SelectedPlugin?.Run();
        private bool CanRunPlugin() => SelectedPlugin != null;

        private void AddPluginByName()
        {
            if (string.IsNullOrWhiteSpace(NewPluginName))
            {
                Frontend.ShowMessageBox("Please enter a plugin name.");
                return;
            }

            var plugin = new PluginModel { Name = NewPluginName };
            LoadedPlugins.Add(plugin);
            SelectedPlugin = plugin;
            NewPluginName = string.Empty;
            SavePlayArea();
        }

        private bool CanAddPlugin() => !string.IsNullOrWhiteSpace(NewPluginName);
        #endregion
    }

    public class PluginModel
    {
        public string Name { get; set; }
        public string Author { get; set; }
        public string Description { get; set; }
        public object Instance { get; set; }
        public string PluginXaml { get; set; }

        public void Run()
        {
            try
            {
                if (Instance is Window win)
                {
                    var newWindow = (Window)Activator.CreateInstance(win.GetType());
                    newWindow.Show();
                    newWindow.Activate();
                }
                else
                {
                    Frontend.ShowMessageBox("Plugin instance is not a Window.");
                }
            }
            catch (Exception ex)
            {
                Frontend.ShowMessageBox($"Plugin run failed: {ex.Message}");
            }
        }
    }
}
