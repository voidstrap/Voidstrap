using System.Windows;
using Voidstrap.AppData;
using Voidstrap.RobloxInterfaces;

namespace Voidstrap.UI.ViewModels.Settings
{
    public class BehaviourViewModel : NotifyPropertyChangedViewModel
    {

        public BehaviourViewModel()
        {
            
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
    }
}
