using System.Windows;
using Voidstrap.UI.ViewModels;
using Voidstrap.UI.ViewModels.ContextMenu;

namespace Voidstrap.UI.Elements.ContextMenu
{
    public partial class RPCWindow
    {
        public RPCWindow()
        {
            InitializeComponent();
            DataContext = new RPCCustomizerViewModel();
        }
    }
}
