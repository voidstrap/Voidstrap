using System.Collections.Generic;
using Voidstrap.AppData;
using Voidstrap.RobloxInterfaces;

namespace Voidstrap.UI.ViewModels.Settings
{
    public class BehaviourViewModel : NotifyPropertyChangedViewModel
    {
        public BehaviourViewModel()
        {
            CleanerItems = new List<string>(App.Settings.Prop.CleanerDirectories);
        }

        public bool MultiInstanceLaunchingEnabled
        {
            get => App.Settings.Prop.MultiInstanceLaunching;
            set
            {
                if (App.Settings.Prop.MultiInstanceLaunching != value)
                {
                    App.Settings.Prop.MultiInstanceLaunching = value;
                    OnPropertyChanged(nameof(MultiInstanceLaunchingEnabled));

                    if (!value)
                    {
                        FixTeleportsEnabled = false;
                    }
                }
            }
        }

        public bool FixTeleportsEnabled
        {
            get => App.Settings.Prop.FixTeleports;
            set
            {
                if (App.Settings.Prop.FixTeleports != value)
                {
                    App.Settings.Prop.FixTeleports = value;
                    OnPropertyChanged(nameof(FixTeleportsEnabled));
                }
            }
        }

        public bool ConfirmLaunches
        {
            get => App.Settings.Prop.ConfirmLaunches;
            set => App.Settings.Prop.ConfirmLaunches = value;
        }

        public bool ForceRobloxLanguage
        {
            get => App.Settings.Prop.ForceRobloxLanguage;
            set => App.Settings.Prop.ForceRobloxLanguage = value;
        }

        public bool RenameClientToEurotrucks2
        {
            get => App.Settings.Prop.RenameClientToEuroTrucks2;
            set => App.Settings.Prop.RenameClientToEuroTrucks2 = value;
        }

        public CleanerOptions SelectedCleanUpMode
        {
            get => App.Settings.Prop.CleanerOptions;
            set => App.Settings.Prop.CleanerOptions = value;
        }

        public IEnumerable<CleanerOptions> CleanerOptions => CleanerOptionsEx.Selections;

        public CleanerOptions CleanerOption
        {
            get => App.Settings.Prop.CleanerOptions;
            set => App.Settings.Prop.CleanerOptions = value;
        }

        private List<string> CleanerItems;

        private void UpdateCleanerItems()
        {
            App.Settings.Prop.CleanerDirectories = new List<string>(CleanerItems);
        }

        public bool CleanerLogs
        {
            get => CleanerItems.Contains("RobloxLogs");
            set
            {
                if (value && !CleanerItems.Contains("RobloxLogs"))
                {
                    CleanerItems.Add("RobloxLogs");
                    UpdateCleanerItems();
                }
                else if (!value && CleanerItems.Contains("RobloxLogs"))
                {
                    CleanerItems.Remove("RobloxLogs");
                    UpdateCleanerItems();
                }
                OnPropertyChanged(nameof(CleanerLogs));
            }
        }

        public bool CleanerCache
        {
            get => CleanerItems.Contains("RobloxCache");
            set
            {
                if (value && !CleanerItems.Contains("RobloxCache"))
                {
                    CleanerItems.Add("RobloxCache");
                    UpdateCleanerItems();
                }
                else if (!value && CleanerItems.Contains("RobloxCache"))
                {
                    CleanerItems.Remove("RobloxCache");
                    UpdateCleanerItems();
                }
                OnPropertyChanged(nameof(CleanerCache));
            }
        }

        public bool CleanerVoidstrap
        {
            get => CleanerItems.Contains("VoidstrapLogs");
            set
            {
                if (value && !CleanerItems.Contains("VoidstrapLogs"))
                {
                    CleanerItems.Add("VoidstrapLogs");
                    UpdateCleanerItems();
                }
                else if (!value && CleanerItems.Contains("VoidstrapLogs"))
                {
                    CleanerItems.Remove("VoidstrapLogs");
                    UpdateCleanerItems();
                }
                OnPropertyChanged(nameof(CleanerVoidstrap));
            }
        }
    }
}
