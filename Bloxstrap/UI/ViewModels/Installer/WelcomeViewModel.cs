namespace Hellstrap.UI.ViewModels.Installer
{
    public class WelcomeViewModel : NotifyPropertyChangedViewModel
    {
        // formatting is done here instead of in xaml, it's just a bit easier
        public string MainText => String.Format(
            Strings.Installer_Welcome_MainText,
            "[github.com/returnrqt/Hellstrap](https://github.com/returnrqt/Hellstrap)"
        );

        public bool CanContinue { get; set; } = false;
    }
}
