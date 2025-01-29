using System.Windows.Media;
using Wpf.Ui.Appearance;

namespace Hellstrap.UI.ViewModels.Bootstrapper
{
    public class FluentDialogViewModel : BootstrapperDialogViewModel
    {
        private static readonly SolidColorBrush LightBackgroundBrush =
            new SolidColorBrush(Color.FromArgb(128, 225, 225, 225));

        private static readonly SolidColorBrush DarkBackgroundBrush =
            new SolidColorBrush(Color.FromArgb(128, 30, 30, 30));

        private static readonly SolidColorBrush TransparentBrush =
            new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));


        public BackgroundType WindowBackdropType { get; set; } = BackgroundType.Mica;
        public SolidColorBrush BackgroundColourBrush { get; set; } = TransparentBrush;
        public string VersionText { get; init; }
        public string ChannelText { get; init; }

        public FluentDialogViewModel(IBootstrapperDialog dialog, bool aero, string version, string channel)
            : base(dialog)
        {
            // Set the backdrop type based on the 'aero' flag
            WindowBackdropType = aero ? BackgroundType.Aero : BackgroundType.Mica;

            // Get the Roblox version
            string realVersion = Utilities.GetRobloxVersion(App.Bootstrapper?.IsStudioLaunch ?? false) ?? "No Version Detected";
            VersionText = $"Version: {realVersion}";

            // Set the background color based on the theme and 'aero' flag
            if (aero)
            {
                BackgroundColourBrush = App.Settings.Prop.Theme.GetFinal() == Enums.Theme.Light
                    ? LightBackgroundBrush
                    : DarkBackgroundBrush;
            }
        }
    }
}
