using System;
using System.Diagnostics;
using System.Windows.Input;
using Voidstrap.UI.ViewModels.ContextMenu;

namespace Voidstrap.UI.Elements.Settings.Pages
{
    public class ExtensionViewModel
    {
        public ExtensionViewModel()
        {
        }

        public bool aniwatchenabler
        {
            get => App.Settings.Prop.AniWatch;
            set => App.Settings.Prop.AniWatch = value;
        }
    }
}
