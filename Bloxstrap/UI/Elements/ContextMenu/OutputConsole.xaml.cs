﻿using Voidstrap.Integrations;
using Voidstrap.UI.ViewModels.ContextMenu;

namespace Voidstrap.UI.Elements.ContextMenu
{
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
