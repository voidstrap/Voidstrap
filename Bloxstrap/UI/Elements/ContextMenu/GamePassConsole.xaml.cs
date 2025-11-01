using Voidstrap.Integrations;
using Voidstrap.UI.Elements.Base;
using Voidstrap.UI.ViewModels.ContextMenu;

namespace Voidstrap.UI.Elements.ContextMenu
{
    public partial class GamePassConsole
    {
        public GamePassConsole(long userId)
        {
            InitializeComponent();
            var vm = new GamePassConsoleViewModel();
            DataContext = vm;
            vm.LoadGamePassesCommand.Execute(userId);
        }
    }
}
