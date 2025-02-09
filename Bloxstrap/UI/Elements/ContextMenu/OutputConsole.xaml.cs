using Hellstrap.Integrations;
using Hellstrap.UI.ViewModels.ContextMenu;

namespace Hellstrap.UI.Elements.ContextMenu
{
    /// <summary>
    /// Interaction logic for OutputConsole.xaml
    /// </summary>
    public partial class OutputConsole
    {
        public OutputConsole(ActivityWatcher watcher)
        {
            var viewModel = new OutputConsoleViewModel(watcher);

            viewModel.RequestCloseEvent += (_, _) => Close();

            DataContext = viewModel;
            InitializeComponent();
        }
    }
}
