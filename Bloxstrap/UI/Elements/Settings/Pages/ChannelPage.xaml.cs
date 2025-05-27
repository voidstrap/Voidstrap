using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using Wpf.Ui.Controls;
using Wpf.Ui.Hardware;
using Voidstrap.UI.Elements.Dialogs;
using Voidstrap.UI.ViewModels.Settings;
using System.Collections.ObjectModel;

namespace Voidstrap.UI.Elements.Settings.Pages
{
    public partial class ChannelPage
    {
        public ChannelPage()
        {
            InitializeComponent();
            DataContext = new ChannelViewModel();
        }


        private void ToggleSwitch_Checked_1(object sender, RoutedEventArgs e)
        {
            HardwareAcceleration.DisableAllAnimations();
            HardwareAcceleration.FreeMemory();
            HardwareAcceleration.OptimizeVisualRendering();
            HardwareAcceleration.DisableTransparencyEffects();
            HardwareAcceleration.MinimizeMemoryFootprint();
        }

        private void ToggleSwitch_Unchecked_1(object sender, RoutedEventArgs e)
        {
            Frontend.ShowMessageBox(
                "Please restart the application to re-enable animations.",
                MessageBoxImage.Warning,
                MessageBoxButton.OK
            );

        }
    }
}
