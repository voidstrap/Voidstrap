using System.Windows;
using System.Windows.Controls;
using Voidstrap.UI.Elements.Base;

namespace Voidstrap.UI.Elements.Dialogs
{
    /// <summary>
    /// Dialog for selecting preset FFlag values
    /// </summary>
    public partial class FFlagPresetsDialog : WpfUiWindow
    {
        public string? SelectedValue { get; private set; }

        private static readonly Dictionary<string, string[]> PresetCategories = new()
        {
            { "Boolean", new[] { "True", "False" } },
            { "Basic Numbers", new[] { "0", "1", "10", "100", "1000" } },
            { "Large Numbers", new[] { "10000", "100000", "1000000", "2147483647" } },
            { "Percentages", new[] { "0", "25", "50", "75", "100" } },
            { "FPS Values", new[] { "30", "60", "120", "144", "240", "360" } },
            { "Quality Levels", new[] { "0", "1", "2", "3", "4", "5", "10", "21" } },
            { "Special Values", new[] { "-1", "null", "\"\"" } },
            { "Memory Values", new[] { "1024", "2048", "4096", "8192", "16384" } }
        };

        public FFlagPresetsDialog()
        {
            InitializeComponent();
            LoadPresetCategories();
        }

        private void LoadPresetCategories()
        {
            foreach (var category in PresetCategories)
            {
                var expander = new Expander
                {
                    Header = category.Key,
                    Margin = new Thickness(0, 5, 0, 5),
                    IsExpanded = category.Key == "Boolean" // Expand Boolean by default
                };

                var stackPanel = new StackPanel();

                foreach (var value in category.Value)
                {
                    var button = new Button
                    {
                        Content = value,
                        Margin = new Thickness(2, 2, 2, 2), // Fixed: provide all 4 values for Thickness
                        Padding = new Thickness(8, 4, 8, 4), // Fixed: provide all 4 values for Thickness
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Transparent),
                        BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray),
                        BorderThickness = new Thickness(1, 1, 1, 1) // Fixed: provide all 4 values for Thickness
                    };

                    button.Click += (s, e) =>
                    {
                        SelectedValue = value;
                        DialogResult = true;
                        Close();
                    };

                    button.MouseEnter += (s, e) =>
                    {
                        button.Background = new System.Windows.Media.SolidColorBrush(
                            System.Windows.Media.Color.FromArgb(50, 100, 149, 237));
                    };

                    button.MouseLeave += (s, e) =>
                    {
                        button.Background = new System.Windows.Media.SolidColorBrush(
                            System.Windows.Media.Colors.Transparent);
                    };

                    stackPanel.Children.Add(button);
                }

                expander.Content = stackPanel;
                PresetStackPanel.Children.Add(expander);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}