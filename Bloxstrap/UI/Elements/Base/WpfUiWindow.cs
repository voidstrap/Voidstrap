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
    public abstract class WpfUiWindow : UiWindow, IDisposable
    {
        private readonly IThemeService _themeService = new ThemeService();
        private ThemeType? _lastAppliedTheme = null;
        private bool _disposed = false;

        public WpfUiWindow()
        {
            ApplyTheme();
        }

        public void ApplyTheme()
        {
            var finalThemeEnum = App.Settings.Prop.Theme.GetFinal();
            var currentTheme = finalThemeEnum == Enums.Theme.Light ? ThemeType.Light : ThemeType.Dark;

            if (_lastAppliedTheme == currentTheme)
                return; // Prevent redundant application

            _lastAppliedTheme = currentTheme;

            _themeService.SetTheme(currentTheme);
            _themeService.SetSystemAccent();

            var themeUri = new Uri($"pack://application:,,,/UI/Style/{Enum.GetName(finalThemeEnum)}.xaml");
            var themeDict = new ResourceDictionary { Source = themeUri };
            ReplaceThemeDictionary(themeDict);

#if QA_BUILD
            this.BorderBrush = System.Windows.Media.Brushes.Red;
            this.BorderThickness = new Thickness(4);
#endif
        }

        private void ReplaceThemeDictionary(ResourceDictionary newDict)
        {
            // Remove existing theme dictionary matching our UI style path
            var existingDict = Application.Current.Resources.MergedDictionaries
                .FirstOrDefault(rd => rd.Source?.ToString().Contains("/UI/Style/") == true);

            if (existingDict != null)
            {
                Application.Current.Resources.MergedDictionaries.Remove(existingDict);
            }

            Application.Current.Resources.MergedDictionaries.Add(newDict);
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            if (App.Settings.Prop.WPFSoftwareRender || App.LaunchSettings.NoGPUFlag.Active)
            {
                if (PresentationSource.FromVisual(this) is HwndSource hwndSource)
                    hwndSource.CompositionTarget.RenderMode = RenderMode.SoftwareOnly;
            }

            base.OnSourceInitialized(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            Dispose();
            base.OnClosed(e);
        }

        // IDisposable implementation to clean up ThemeService if needed
        public void Dispose()
        {
            if (_disposed)
                return;

            if (_themeService is IDisposable disposable)
            {
                disposable.Dispose();
            }

            _disposed = true;
        }
    }
}
