using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;

namespace Voidstrap.UI.ViewModels.Settings
{
    public partial class NewsItem : ObservableObject
    {
        [ObservableProperty]
        private string title = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsNew))]
        [NotifyPropertyChangedFor(nameof(AgeLabel))]
        private DateTime date;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Tags))]
        [NotifyPropertyChangedFor(nameof(DisplayContent))]
        private string content = string.Empty;

        [ObservableProperty]
        private string imageUrl = string.Empty;

        [ObservableProperty]
        private BitmapImage? image;
        public ObservableCollection<string> Tags =>
            new(Regex.Matches(content ?? string.Empty, @"(https?://[^\s]+)")
                .Select(m => m.Value.TrimEnd('.', ',', ')'))
                .Where(u => Uri.IsWellFormedUriString(u, UriKind.Absolute))
                .Distinct()
                .ToList());
        public string DisplayContent =>
            Regex.Replace(content ?? string.Empty, @"https?://[^\s]+", "").Trim();
        public bool IsNew =>
            (DateTime.UtcNow - Date.ToUniversalTime()).TotalHours < 24;
        public string AgeLabel => IsNew ? "NEW" : "OLD";
    }
}
