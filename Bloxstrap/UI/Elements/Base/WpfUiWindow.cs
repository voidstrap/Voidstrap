using System;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Wpf.Ui.Mvvm.Contracts;
using Wpf.Ui.Mvvm.Services;

namespace Voidstrap.UI.Elements.Base
{
    /// <summary>
    /// Base window class that integrates theme management and software rendering control.
    /// </summary>
    public abstract class WpfUiWindow : UiWindow, IDisposable
    {
        private readonly IThemeService _themeService = new ThemeService();
        private ThemeType? _lastAppliedTheme = null;
        private bool _disposed = false;

        protected WpfUiWindow()
        {
            ApplyTheme();
        }

        /// <summary>
        /// Applies the current theme based on application settings.
        /// </summary>
        public void ApplyTheme()
        {
            var finalThemeEnum = App.Settings.Prop.Theme2.GetFinal();
            var currentTheme = finalThemeEnum == Enums.Theme.Light ? ThemeType.Light : ThemeType.Dark;

            if (_lastAppliedTheme == currentTheme)
                return;

            _lastAppliedTheme = currentTheme;

            _themeService.SetTheme(currentTheme);
            _themeService.SetSystemAccent();

            var themeName = Enum.GetName(typeof(Enums.Theme), finalThemeEnum);
            if (themeName != null)
            {
                var themeUri = new Uri($"pack://application:,,,/UI/Style/{themeName}.xaml");
                ReplaceThemeDictionary(new ResourceDictionary { Source = themeUri });
            }

#if QA_BUILD
            BorderBrush = System.Windows.Media.Brushes.Red;
            BorderThickness = new Thickness(4);
#endif
        }

        /// <summary>
        /// Replaces the current theme resource dictionary with a new one.
        /// </summary>
        /// <param name="newDict">The new ResourceDictionary to apply.</param>
        private void ReplaceThemeDictionary(ResourceDictionary newDict)
        {
            if (Application.Current == null)
                return;

            var resources = Application.Current.Resources.MergedDictionaries;
            var existingDict = resources.FirstOrDefault(rd =>
                rd.Source?.ToString().Contains("/UI/Style/") == true);

            if (existingDict != null)
            {
                resources.Remove(existingDict);
            }

            resources.Add(newDict);
        }

        /// <summary>
        /// Enables software rendering if configured in settings.
        /// </summary>
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            if (App.Settings.Prop.WPFSoftwareRender || App.LaunchSettings.NoGPUFlag.Active)
            {
                if (PresentationSource.FromVisual(this) is HwndSource hwndSource)
                {
                    hwndSource.CompositionTarget.RenderMode = RenderMode.SoftwareOnly;
                }
            }
        }

        /// <summary>
        /// Cleans up resources on window close.
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            Dispose();
            base.OnClosed(e);
        }

        /// <summary>
        /// Disposes the window and internal services.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            if (_themeService is IDisposable disposable)
                disposable.Dispose();

            _disposed = true;
        }
    }
}
