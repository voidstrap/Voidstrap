using Voidstrap.Integrations;
using Voidstrap.UI.Elements.Base;
using Voidstrap.UI.ViewModels.ContextMenu;

namespace Voidstrap.UI.Elements.ContextMenu
{
    public partial class BetterBloxDataCenterConsole
    {
        public BetterBloxDataCenterConsole()
        {
            InitializeComponent();
            var vm = new BetterBloxDataCenterConsoleViewModel();
            DataContext = vm;
        }
    }
}
