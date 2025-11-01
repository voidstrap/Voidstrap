﻿using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Voidstrap.UI.Elements.About;

namespace Voidstrap.UI.ViewModels.Settings
{
    public class MainWindowViewModel : NotifyPropertyChangedViewModel
    {

        public ICommand OpenAboutCommand => new RelayCommand(OpenAbout);

        public ICommand SaveSettingsCommand => new RelayCommand(SaveSettings);

        public ICommand SaveAndLaunchSettingsCommand => new RelayCommand(SaveAndLaunchSettings);

        public event PropertyChangedEventHandler? PropertyChanged;

        public ICommand CloseWindowCommand => new RelayCommand(CloseWindow);

        public EventHandler? RequestSaveNoticeEvent;
        public EventHandler? RequestSaveLaunchNoticeEvent;

        public EventHandler? RequestCloseWindowEvent;

        public bool TestModeEnabled
        {
            get => App.LaunchSettings.TestModeFlag.Active;
            set
            {
                if (value && !App.State.Prop.TestModeWarningShown)
                {
                    var result = Frontend.ShowMessageBox(Strings.Menu_TestMode_Prompt, MessageBoxImage.Information, MessageBoxButton.YesNo);

                    if (result != MessageBoxResult.Yes)
                        return;

                    App.State.Prop.TestModeWarningShown = true;
                }

                App.LaunchSettings.TestModeFlag.Active = value;
            }
        }

        private void OpenAbout() => new MainWindow().ShowDialog();

        private void CloseWindow() => RequestCloseWindowEvent?.Invoke(this, EventArgs.Empty);

        private void SaveSettings()
        {
            const string LOG_IDENT = "MainWindowViewModel::SaveSettings";

            App.Settings.Save();
            App.State.Save();
            App.FastFlags.Save();

            foreach (var pair in App.PendingSettingTasks)
            {
                var task = pair.Value;

                if (task.Changed)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Executing pending task '{task}'");
                    task.Execute();
                }
            }

            App.PendingSettingTasks.Clear();

            RequestSaveNoticeEvent?.Invoke(this, EventArgs.Empty);
        }

        public void SaveAndLaunchSettings()
        {
            SaveSettings();
            RequestSaveLaunchNoticeEvent?.Invoke(this, EventArgs.Empty);
            LaunchHandler.LaunchRoblox(LaunchMode.Player);
        }

        protected bool SetProperty<T>(ref T field, T newValue, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, newValue))
            {
                return false;
            }

            field = newValue;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName ?? throw new ArgumentNullException(nameof(propertyName))));

            return true;
        }
    }
}
