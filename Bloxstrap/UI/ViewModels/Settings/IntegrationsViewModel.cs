using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Xml.Linq;
using Voidstrap.Integrations;
using Voidstrap.UI.Elements.ContextMenu;
using Wpf.Ui.Appearance;

namespace Voidstrap.UI.ViewModels.Settings
{
    public class IntegrationsViewModel : NotifyPropertyChangedViewModel
    {
        public ICommand AddIntegrationCommand => new RelayCommand(AddIntegration);
        public ICommand DeleteIntegrationCommand => new RelayCommand(DeleteIntegration);
        public ICommand BrowseIntegrationLocationCommand => new RelayCommand(BrowseIntegrationLocation);
        public ICommand OpenHistoryWindowCommand { get; }
        public ICommand MusicWindowCommand { get; }
        public ICommand RPCWindowCommand { get; }
        public ICommand AccountWindowCommand { get; }

        private readonly ActivityWatcher _watcher;

        public IntegrationsViewModel(ActivityWatcher watcher)
        {
            _watcher = watcher;
            OpenHistoryWindowCommand = new RelayCommand(OpenHistoryWindow);
            MusicWindowCommand = new RelayCommand(MusicPlayerWindow);
            RPCWindowCommand = new RelayCommand(RPCUIWindow);
            AccountWindowCommand = new RelayCommand(AccountWindow);
        }

        private void AddIntegration()
        {
            CustomIntegrations.Add(new CustomIntegration()
            {
                Name = Strings.Menu_Integrations_Custom_NewIntegration
            });

            SelectedCustomIntegrationIndex = CustomIntegrations.Count - 1;

            OnPropertyChanged(nameof(SelectedCustomIntegrationIndex));
            OnPropertyChanged(nameof(IsCustomIntegrationSelected));
        }

        private void OpenHistoryWindow()
        {
            var historyWindow = new ServerHistory(_watcher);
            historyWindow.Show();
        }

        private void MusicPlayerWindow()
        {
            var musicPlayerWindow = new MusicPlayer(_watcher);
            musicPlayerWindow.Show();
        }

        private void RPCUIWindow()
        {
            var rpcUIWindow = new RPCWindow();
            rpcUIWindow.Show();
        }

        private void DeleteIntegration()
        {
            if (SelectedCustomIntegration is null)
                return;

            CustomIntegrations.Remove(SelectedCustomIntegration);

            if (CustomIntegrations.Count > 0)
            {
                SelectedCustomIntegrationIndex = CustomIntegrations.Count - 1;
                OnPropertyChanged(nameof(SelectedCustomIntegrationIndex));
            }

            OnPropertyChanged(nameof(IsCustomIntegrationSelected));
        }

        private void BrowseIntegrationLocation()
        {
            if (SelectedCustomIntegration is null)
                return;

            var dialog = new OpenFileDialog
            {
                Filter = $"{Strings.Menu_AllFiles}|*.*"
            };

            if (dialog.ShowDialog() != true)
                return;

            SelectedCustomIntegration.Name = dialog.SafeFileName;
            SelectedCustomIntegration.Location = dialog.FileName;
            OnPropertyChanged(nameof(SelectedCustomIntegration));
        }

        public bool ActivityTrackingEnabled
        {
            get => App.Settings.Prop.EnableActivityTracking;

            set
            {
                App.Settings.Prop.EnableActivityTracking = value;

                if (!value)
                {
                    ShowServerDetailsEnabled = value;
                    DisableAppPatchEnabled = value;
                    DiscordActivityEnabled = value;
                    DiscordActivityJoinEnabled = value;

                    OnPropertyChanged(nameof(ShowServerDetailsEnabled));
                    OnPropertyChanged(nameof(DisableAppPatchEnabled));
                    OnPropertyChanged(nameof(DiscordActivityEnabled));
                    OnPropertyChanged(nameof(DiscordActivityJoinEnabled));
                }
            }
        }

        public bool ShowServerDetailsEnabled
        {
            get => App.Settings.Prop.ShowServerDetails;
            set => App.Settings.Prop.ShowServerDetails = value;
        }

        public bool exitondissy
        {
            get => App.Settings.Prop.exitondissy;
            set => App.Settings.Prop.exitondissy = value;
        }

        public string gamename
        {
            get => App.Settings.Prop.CustomGameName;
            set => App.Settings.Prop.CustomGameName = value;
        }

        public bool GameWIP
        {
            get => App.Settings.Prop.GameWIP;
            set => App.Settings.Prop.GameWIP = value;
        }

        public bool ServerUptimeBetterBLOXcuzitsbetterXD
        {
            get => App.Settings.Prop.ServerUptimeBetterBLOXcuzitsbetterXD;
            set => App.Settings.Prop.ServerUptimeBetterBLOXcuzitsbetterXD = value;
        }

        private void AccountWindow()
        {
            var accountWindow = new AccountManagerWindow();
            accountWindow.Show();
        }

        public string gameimage
        {
            get => App.Settings.Prop.UseCustomIcon;
            set => App.Settings.Prop.UseCustomIcon = value;
        }

        public bool PlayerLogsEnabled
        {
            get => App.FastFlags.GetPreset("Players.LogLevel") == "trace";
            set
            {
                App.FastFlags.SetPreset("Players.LogLevel", value ? "trace" : null);
                App.FastFlags.SetPreset("Players.LogPattern", value ? "ExpChat/mountClientApp" : null);
            }
        }

        public bool DiscordActivityEnabled
        {
            get => App.Settings.Prop.UseDiscordRichPresence;
            set
            {
                App.Settings.Prop.UseDiscordRichPresence = value;

                if (!value)
                {
                    DiscordActivityJoinEnabled = value;
                    DiscordAccountOnProfile = value;
                    GameIconChecked = value;
                    ServerLocationGame = value;
                    OnPropertyChanged(nameof(DiscordActivityJoinEnabled));
                    OnPropertyChanged(nameof(DiscordAccountOnProfile));
                    OnPropertyChanged(nameof(GameIconChecked));
                    OnPropertyChanged(nameof(ServerLocationGame));
                }
            }
        }

        public bool UncapFPS
        {
            get => RobloxSettings.IsUncapped();
            set => RobloxSettings.SetUncapped(value);
        }

        public bool DiscordActivityJoinEnabled
        {
            get => !App.Settings.Prop.HideRPCButtons;
            set => App.Settings.Prop.HideRPCButtons = !value;
        }

        public bool DiscordAccountOnProfile
        {
            get => App.Settings.Prop.ShowAccountOnRichPresence;
            set => App.Settings.Prop.ShowAccountOnRichPresence = value;
        }

        public bool GameIconChecked
        {
            get => App.Settings.Prop.GameIconChecked;
            set => App.Settings.Prop.GameIconChecked = value;
        }
        public bool GameNameChecked
        {
            get => App.Settings.Prop.GameNameChecked;
            set => App.Settings.Prop.GameNameChecked = value;
        }

        public bool GameCreatorChecked
        {
            get => App.Settings.Prop.GameCreatorChecked;
            set => App.Settings.Prop.GameCreatorChecked = value;
        }
        public bool GameStatusChecked
        {
            get => App.Settings.Prop.GameStatusChecked;
            set => App.Settings.Prop.GameStatusChecked = value;
        }

        public bool ServerLocationGame
        {
            get => App.Settings.Prop.ServerLocationGame;
            set => App.Settings.Prop.ServerLocationGame = value;
        }

        public bool DisableAppPatchEnabled
        {
            get => App.Settings.Prop.UseDisableAppPatch;
            set => App.Settings.Prop.UseDisableAppPatch = value;
        }

        public ObservableCollection<CustomIntegration> CustomIntegrations
        {
            get => App.Settings.Prop.CustomIntegrations;
            set => App.Settings.Prop.CustomIntegrations = value;
        }

        public CustomIntegration? SelectedCustomIntegration { get; set; }
        public int SelectedCustomIntegrationIndex { get; set; }
        public bool IsCustomIntegrationSelected => SelectedCustomIntegration is not null;
    }
}