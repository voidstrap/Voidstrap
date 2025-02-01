using System.Windows.Media;
using Wpf.Ui.Appearance;

namespace Hellstrap.UI.ViewModels.Bootstrapper
{
    public class FluentDialogViewModel : BootstrapperDialogViewModel
    {
        // Defining background color brushes
        private static readonly SolidColorBrush LightBackgroundBrush =
            new SolidColorBrush(Color.FromArgb(128, 225, 225, 225));

        private static readonly SolidColorBrush DarkBackgroundBrush =
            new SolidColorBrush(Color.FromArgb(128, 30, 30, 30));

        private static readonly SolidColorBrush TransparentBrush =
            new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));

        // Properties
        public BackgroundType WindowBackdropType { get; private set; } = BackgroundType.Mica;
        public SolidColorBrush BackgroundColourBrush { get; private set; } = TransparentBrush;
        public string VersionText { get; private set; }
        public string ChannelText { get; private set; }

        // Constructor
        public FluentDialogViewModel(IBootstrapperDialog dialog, bool isAero, string version, string channel)
            : base(dialog)
        {
            SetBackdropType(isAero);
            SetVersionText();
            SetBackgroundColor(isAero);
            ChannelText = channel;  // Assuming 'channel' is directly assigned
        }

        // Method to determine the backdrop type
        private void SetBackdropType(bool isAero)
        {
            WindowBackdropType = isAero ? BackgroundType.Aero : BackgroundType.Mica;
        }

        // Method to set the version text
        private void SetVersionText()
        {
            string realVersion = Utilities.GetRobloxVersion(App.Bootstrapper?.IsStudioLaunch ?? false) ?? "No Version Detected";
            VersionText = $"Version: {realVersion}";
        }

        // Method to set the background color based on the theme and 'aero' flag
        private void SetBackgroundColor(bool isAero)
        {
            if (isAero)
            {
                var currentTheme = App.Settings.Prop.Theme.GetFinal();
                BackgroundColourBrush = currentTheme == Enums.Theme.Light ? LightBackgroundBrush : DarkBackgroundBrush;
            }
        }
    }
}
