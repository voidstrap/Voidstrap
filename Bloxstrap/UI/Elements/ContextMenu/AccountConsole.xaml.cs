using System.Windows;
using Voidstrap.UI.ViewModels;

namespace Voidstrap.UI.Elements.ContextMenu
{
    public partial class AccountManagerWindow
    {
        public AccountManagerWindow()
        {
            InitializeComponent();
            DataContext = new AccountBackupsViewModel();
        }
    }
}
