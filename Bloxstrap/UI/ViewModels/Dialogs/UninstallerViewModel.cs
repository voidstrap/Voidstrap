using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

using Voidstrap.Resources;
using Voidstrap;

namespace Voidstrap.UI.ViewModels.Dialogs
{
    public class UninstallerViewModel
    {
        public string Text => String.Format(
            Strings.Uninstaller_Text,
            "https://github.com/BloxstrapLabs/Bloxstrap/wiki/Roblox-crashes-or-does-not-launch",
            Paths.Base
        );

        public bool KeepData { get; set; } = true;

        public ICommand ConfirmUninstallCommand => new RelayCommand(ConfirmUninstall);

        public event EventHandler? ConfirmUninstallRequest;

        private void ConfirmUninstall() => ConfirmUninstallRequest?.Invoke(this, new EventArgs());
    }
}