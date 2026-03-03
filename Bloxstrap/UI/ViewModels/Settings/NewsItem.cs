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
        private static readonly Regex UrlRegex =
            new(@"(https?://[^\s]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex UrlStripRegex =
            new(@"https?://[^\s]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        [ObservableProperty]
        private string title = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsNew))]
        [NotifyPropertyChangedFor(nameof(AgeLabel))]
        private DateTime date;

        [ObservableProperty]
        private string content = string.Empty;

        [ObservableProperty]
        private string imageUrl = string.Empty;

        [ObservableProperty]
        private BitmapImage? image;

        private readonly ObservableCollection<string> tags = new();
        public ObservableCollection<string> Tags => tags;

        partial void OnContentChanged(string value)
        {
            GenerateTags(value);
            OnPropertyChanged(nameof(DisplayContent));
        }

        private void GenerateTags(string? text)
        {
            tags.Clear();

            if (string.IsNullOrWhiteSpace(text))
                return;

            var matches = UrlRegex.Matches(text);

            foreach (var url in matches
                .Select(m => m.Value.TrimEnd('.', ',', ')'))
                .Where(u => Uri.IsWellFormedUriString(u, UriKind.Absolute))
                .Distinct())
            {
                tags.Add(url);
            }
        }

        public string DisplayContent =>
            string.IsNullOrWhiteSpace(Content)
                ? string.Empty
                : UrlStripRegex.Replace(Content, "").Trim();

        public bool IsNew =>
            (DateTime.Now - Date).TotalDays <= 3;

        public string AgeLabel => "NEW";
    }
}