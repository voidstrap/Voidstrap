using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;

namespace Voidstrap.UI.Elements.ContextMenu
{
    public partial class CustomThemeEditor
    {
        private readonly string _path = Path.Combine(Paths.Base, "Custom.xaml");
        private TextMarkerService _textMarkerService;
        private int _lastErrorLine = -1;

        public CustomThemeEditor()
        {
            InitializeComponent();
            _textMarkerService = new TextMarkerService(CodeEditor.Document);
            CodeEditor.TextArea.TextView.BackgroundRenderers.Add(_textMarkerService);
            CodeEditor.TextArea.TextView.LineTransformers.Add(_textMarkerService);
            CodeEditor.TextArea.TextView.Services.AddService(typeof(ITextMarkerService), _textMarkerService);

            LoadTheme();
        }

        private void Log(string message)
        {
            LogConsole.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
            LogConsole.ScrollToEnd();
        }

        private void ClearErrorMarkers()
        {
            if (_textMarkerService != null)
            {
                _textMarkerService.RemoveAll(m => true);
                _lastErrorLine = -1;
            }
        }

        private void LoadTheme()
        {
            try
            {
                if (!File.Exists(_path))
                {
                    Directory.CreateDirectory(Paths.Base);
                    File.WriteAllText(_path, DefaultTheme());
                    Log("Created default theme.");
                }

                CodeEditor.Text = File.ReadAllText(_path);
                Log("Theme loaded.");
            }
            catch (Exception ex)
            {
                Log($"LoadTheme Error: {ex.Message}");
            }
        }

        private string GetBackupFolder()
        {
            string folder = Path.Combine(Paths.Base, "Backups");
            Directory.CreateDirectory(folder);
            return folder;
        }

        private string GetBackupPath()
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            return Path.Combine(GetBackupFolder(), $"Custom_{timestamp}.xaml");
        }

        private void CleanupOldBackups(int maxBackups = 5)
        {
            var folder = GetBackupFolder();
            var files = new DirectoryInfo(folder)
                        .GetFiles("Custom_*.xaml")
                        .OrderByDescending(f => f.CreationTime)
                        .ToList();

            for (int i = maxBackups; i < files.Count; i++)
            {
                try { files[i].Delete(); } catch { }
            }
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string xaml = CodeEditor.Text;
                if (string.IsNullOrWhiteSpace(xaml))
                {
                    Frontend.ShowMessageBox("Theme is empty.");
                    return;
                }

                if (File.Exists(_path))
                {
                    string backupPath = GetBackupPath();
                    File.Copy(_path, backupPath, true);
                    CleanupOldBackups();
                }

                ResourceDictionary dict = ParseXamlSafe(xaml);
                if (dict == null)
                {
                    return;
                }

                var merged = Application.Current.Resources.MergedDictionaries;
                var old = merged.FirstOrDefault(d =>
                    d.Source != null &&
                    d.Source.OriginalString.EndsWith("Custom.xaml", StringComparison.OrdinalIgnoreCase));

                if (old != null)
                    merged.Remove(old);

                merged.Add(dict);
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Render);

                File.WriteAllText(_path, xaml);
                Log("Applied new theme.");
            }
            catch (Exception ex)
            {
                Log($"Failed to apply theme:\n{ex.Message}");

                var backupFiles = new DirectoryInfo(GetBackupFolder())
                                  .GetFiles("Custom_*.xaml")
                                  .OrderByDescending(f => f.CreationTime)
                                  .ToArray();

                if (backupFiles.Length == 0)
                {
                    Log("No backups available to revert.");
                    return;
                }

                string latestBackup = backupFiles[0].FullName;
                    try
                    {
                        string backupXaml = File.ReadAllText(latestBackup);
                        ResourceDictionary dict = ParseXamlSafe(backupXaml);

                        if (dict != null)
                        {
                            var merged = Application.Current.Resources.MergedDictionaries;
                            var old = merged.FirstOrDefault(d =>
                                d.Source != null &&
                                d.Source.OriginalString.EndsWith("Custom.xaml", StringComparison.OrdinalIgnoreCase));

                            if (old != null) merged.Remove(old);
                            merged.Add(dict);

                            File.WriteAllText(_path, backupXaml);
                            CodeEditor.Text = backupXaml;
                            Log("Reverted to latest backup.");
                        }
                        else
                        {
                            Log("Backup is corrupted. Cannot revert.");
                        }
                    }
                    catch (Exception revertEx)
                    {
                        Log($"Failed to revert theme: {revertEx.Message}");
                    }
                }
            }
        
        private void CopyErrors_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_lastErrorLine <= 0)
                {
                    return;
                }

                var lines = LogConsole.Text.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                var lastErrorIndex = Array.LastIndexOf(lines, lines.LastOrDefault(l => l.StartsWith("❌")));

                string errorText;
                if (lastErrorIndex >= 0)
                {
                    var errorLines = lines.Skip(lastErrorIndex).ToArray();
                    errorText = string.Join("\n", errorLines);
                }
                else
                {
                    errorText = "Error occurred, but no details found in the log.";
                }

                errorText = $"Error at line {_lastErrorLine}:\n{errorText}";
                Clipboard.SetText(errorText);
            }
            catch (Exception ex)
            {
                Log($"CopyErrors_Click Exception:\n  {ex.Message}");
            }
        }

        private void Reload_Click(object sender, RoutedEventArgs e)
        {
            Directory.CreateDirectory(Paths.Base);
            File.WriteAllText(_path, DefaultTheme());
            Log("Reloaded.");
            LoadTheme();
        }

        private void PickColor_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new System.Windows.Forms.ColorDialog();

            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string hex = $"#{dlg.Color.A:X2}{dlg.Color.R:X2}{dlg.Color.G:X2}{dlg.Color.B:X2}";
                InsertOrReplaceHex(hex);
                Log($"{hex}");
            }
        }

        private void InsertOrReplaceHex(string hex)
        {
            var regex = new Regex("#[A-Fa-f0-9]{6,8}");
            var caret = CodeEditor.CaretOffset;
            var text = CodeEditor.Text;

            var match = regex.Matches(text)
                             .Cast<Match>()
                             .FirstOrDefault(m => caret >= m.Index && caret <= m.Index + m.Length);

            if (match != null)
            {
                CodeEditor.Text = text.Remove(match.Index, match.Length)
                                      .Insert(match.Index, hex);
                CodeEditor.CaretOffset = match.Index + hex.Length;
            }
            else
            {
                CodeEditor.Document.Insert(caret, hex);
            }
        }

        private ResourceDictionary ParseXamlSafe(string xaml)
        {
            try
            {
                using var ms = new MemoryStream(Encoding.UTF8.GetBytes(xaml));
                var dict = (ResourceDictionary)XamlReader.Load(ms);

                ClearErrorMarkers();
                return dict;
            }
            catch (XamlParseException ex)
            {
                Log($"XAML Parse Error:\n  Line: {ex.LineNumber}, Position: {ex.LinePosition}\n  Message: {ex.Message}\n  Exception Type: {ex.GetType().FullName}");

                _lastErrorLine = ex.LineNumber;
                HighlightErrorLine(ex.LineNumber);

                return null;
            }
            catch (Exception ex)
            {
                Log($"General Exception:\n  Message: {ex.Message}\n  Type: {ex.GetType().FullName}\n  StackTrace: {ex.StackTrace}");
                _lastErrorLine = CodeEditor.TextArea.Caret.Line;
                HighlightErrorLine(_lastErrorLine);
                return null;
            }
        }

        private void HighlightErrorLine(int lineNumber)
        {
            if (lineNumber <= 0 || CodeEditor.Document == null || _textMarkerService == null)
                return;

            try
            {
                var line = CodeEditor.Document.GetLineByNumber(lineNumber);
                _textMarkerService.RemoveAll(m => true);

                var marker = _textMarkerService.Create(line.Offset, line.Length);
                marker.BackgroundColor = Colors.Red;
                marker.ForegroundColor = Colors.White;
                marker.ToolTip = "XAML Error on this line";

                _lastErrorLine = lineNumber;

                CodeEditor.ScrollTo(lineNumber, 0);
                CodeEditor.CaretOffset = line.Offset;
            }
            catch
            {
            }
        }

        private string DefaultTheme() =>
@"<ResourceDictionary xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
                    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
                    xmlns:base=""clr-namespace:Voidstrap.UI.Elements.Base"">

    <SolidColorBrush x:Key=""NewTextEditorBackground"" Color=""#CC1E1E1E"" />
    <SolidColorBrush x:Key=""NewTextEditorForeground"" Color=""#FF1C1C1C"" />
    <SolidColorBrush x:Key=""NewTextEditorLink"" Color=""#FF3897E8"" />
    
    <SolidColorBrush x:Key=""PrimaryBackgroundColor"" Color=""#FF1C1C1C"" />
    
    <Color x:Key=""ControlFillColorDefault"">#CC1E1E1E</Color>
    
    <Color x:Key=""WindowBackgroundColorPrimary"">#CC1E1E1E</Color>
    <Color x:Key=""WindowBackgroundColorSecondary"">#CC2A2A2A</Color>
    
    <Color x:Key=""WindowBackgroundColorThird"">#CC1E1E1E</Color>

</ResourceDictionary>";
    }
}
