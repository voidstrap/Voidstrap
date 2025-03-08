using System.Windows;
using Hellstrap.AppData;
using Hellstrap.RobloxInterfaces;

namespace Hellstrap.UI.ViewModels.Settings
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
                App.Settings.Prop.MultiInstanceLaunching = value;

                if (!value)
                {
                    FixTeleportsEnabled = value;
                    OnPropertyChanged(nameof(FixTeleportsEnabled));
                }
            }
        }

        public bool FixTeleportsEnabled
        {
            get => App.Settings.Prop.FixTeleports;
            set
            {


                App.Settings.Prop.FixTeleports = value;
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
