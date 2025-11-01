using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Voidstrap.Integrations;
using Voidstrap.UI.Elements.Base;
using Voidstrap.UI.ViewModels.ContextMenu;

namespace Voidstrap.UI.Elements.ContextMenu
{
    public partial class MusicPlayer : WpfUiWindow
    {
        private readonly ActivityWatcher? _activityWatcher;
        private readonly MusicPlayerViewModel _viewModel;

        public MusicPlayer() : this(null) { }

        public MusicPlayer(ActivityWatcher? activityWatcher)
        {
            InitializeComponent();
            _activityWatcher = activityWatcher;

            _viewModel = new MusicPlayerViewModel();
            DataContext = _viewModel;
            Loaded += MusicPlayer_Loaded;
        }

        private void TrackTitleEdit_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && sender is TextBox textBox)
            {
                Keyboard.ClearFocus();
                e.Handled = true;
            }
        }

        private void MusicPlayer_Loaded(object sender, RoutedEventArgs e)
        {
            if (FindName("SearchBox") is TextBox searchBox)
            {
                searchBox.TextChanged += (s, ev) =>
                {
                    _viewModel.SearchQuery = searchBox.Text;
                    UpdateFilteredLibrary();
                };
            }
            UpdateFilteredLibrary();
        }
        private void UpdateFilteredLibrary()
        {
            var query = _viewModel.SearchQuery?.ToLower() ?? string.Empty;

            var filtered = _viewModel.MusicLibrary
                .Where(t =>
                    (!string.IsNullOrEmpty(t.Title) && t.Title.ToLower().Contains(query)) ||
                    (!string.IsNullOrEmpty(t.Artist) && t.Artist.ToLower().Contains(query)))
                .ToList();

            _viewModel.FilteredMusicLibrary.Clear();
            foreach (var item in filtered)
                _viewModel.FilteredMusicLibrary.Add(item);
        }
    }
}
