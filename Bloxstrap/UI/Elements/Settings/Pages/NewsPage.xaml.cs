using Wpf.Ui.Controls;
using Voidstrap.UI.ViewModels.Settings;
using System.Diagnostics;
using System.Windows;

namespace Voidstrap.UI.Elements.Settings.Pages
{
    public partial class NewsPage : UiPage
    {
        private readonly NewsViewModel _viewModel = new();

        public NewsPage()
        {
            InitializeComponent();
            Loaded += (_, _) =>
            {
                if (DataContext is not NewsViewModel)
                {
                    DataContext = _viewModel;
                    Debug.WriteLine($"[NewsPage] DataContext attached to: {DataContext.GetType().FullName}");
                }
                else
                {
                    Debug.WriteLine("[NewsPage] DataContext already correct");
                }
            };
        }
    }
}
