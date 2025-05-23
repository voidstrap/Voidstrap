﻿using Voidstrap.Integrations;
using Voidstrap.UI.ViewModels.ContextMenu;

namespace Voidstrap.UI.Elements.ContextMenu
{
    /// <summary>
    /// Interaction logic for ChatLogs.xaml
    /// </summary>
    public partial class ChatLogs
    {
        public ChatLogs(ActivityWatcher watcher)
        {
            var viewModel = new ChatLogsViewModel(watcher);

            viewModel.RequestCloseEvent += (_, _) => Close();

            DataContext = viewModel;
            InitializeComponent();
        }

        private void ChatLogsDataGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {

        }
    }
}