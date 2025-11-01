using System;
using System.Collections.Generic;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Voidstrap.Integrations;
using Voidstrap.UI.ViewModels;

namespace Voidstrap.UI.ViewModels.ContextMenu
{
    internal class ChatLogsViewModel : NotifyPropertyChangedViewModel
    {
        private readonly ActivityWatcher _activityWatcher;

        public Dictionary<int, ActivityData.UserMessage>? MessageLogs { get; private set; }

        public IEnumerable<KeyValuePair<int, ActivityData.UserMessage>>? MessageLogsCollection => MessageLogs;

        public GenericTriState LoadState { get; private set; } = GenericTriState.Unknown;

        public string Error { get; private set; } = string.Empty;

        public ICommand CloseWindowCommand { get; }

        public event EventHandler? RequestCloseEvent;

        public ChatLogsViewModel(ActivityWatcher activityWatcher)
        {
            _activityWatcher = activityWatcher ?? throw new ArgumentNullException(nameof(activityWatcher));

            CloseWindowCommand = new RelayCommand(RequestClose);

            _activityWatcher.OnNewMessageRequest += (_, _) => LoadData();

            LoadData();
        }

        private void LoadData()
        {
            try
            {
                LoadState = GenericTriState.Unknown;
                OnPropertyChanged(nameof(LoadState));

                MessageLogs = new Dictionary<int, ActivityData.UserMessage>(_activityWatcher.MessageLogs);

                OnPropertyChanged(nameof(MessageLogs));
                OnPropertyChanged(nameof(MessageLogsCollection));

                LoadState = GenericTriState.Successful;
            }
            catch (Exception ex)
            {
                Error = ex.Message;
                LoadState = GenericTriState.Failed;
                OnPropertyChanged(nameof(Error));
            }
            finally
            {
                OnPropertyChanged(nameof(LoadState));
            }
        }

        private void RequestClose() => RequestCloseEvent?.Invoke(this, EventArgs.Empty);
    }
}
