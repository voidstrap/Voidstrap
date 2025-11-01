using System.Windows;
using System.Windows.Input;
using Voidstrap.UI.ViewModels.Settings;
using Wpf.Ui.Mvvm.Contracts;

namespace Voidstrap.UI.Elements.Settings.Pages
{
    public partial class GBSEditorPage
    {
        private GBSEditorViewModel _viewModel = null!;

        public GBSEditorPage()
        {
            SetupViewModel();
            InitializeComponent();
        }

        private void SetupViewModel()
        {
            _viewModel = new GBSEditorViewModel();
            DataContext = _viewModel;
        }

        private void ValidateUInt32(object sender, TextCompositionEventArgs e) => e.Handled = !UInt32.TryParse(e.Text, out uint _);
        private void ValidateFloat(object sender, TextCompositionEventArgs e) => e.Handled = !float.TryParse(e.Text, out float _);
    }
}