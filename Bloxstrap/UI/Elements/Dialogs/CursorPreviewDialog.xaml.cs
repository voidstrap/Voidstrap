using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Voidstrap.Enums;
using Voidstrap.UI.Elements.Base;

namespace Voidstrap.UI.Elements.Dialogs
{
    /// <summary>
    /// Dialog for previewing cursor types before applying them
    /// </summary>
    public partial class CursorPreviewDialog : WpfUiWindow
    {
        public Voidstrap.Enums.CursorType? SelectedCursor { get; private set; }

        public CursorPreviewDialog()
        {
            InitializeComponent();
            LoadCursorPreviews();
        }

        private void LoadCursorPreviews()
        {
            var cursors = new[]
            {
                Voidstrap.Enums.CursorType.Default,
                Voidstrap.Enums.CursorType.FPSCursor,
                Voidstrap.Enums.CursorType.CleanCursor,
                Voidstrap.Enums.CursorType.DotCursor,
                Voidstrap.Enums.CursorType.StoofsCursor,
                Voidstrap.Enums.CursorType.From2006,
                Voidstrap.Enums.CursorType.From2013,
                Voidstrap.Enums.CursorType.WhiteDotCursor,
                Voidstrap.Enums.CursorType.VerySmallWhiteDot
            };

            foreach (var cursor in cursors)
            {
                var previewItem = CreateCursorPreviewItem(cursor);
                CursorStackPanel.Children.Add(previewItem);
            }
        }

        private FrameworkElement CreateCursorPreviewItem(Voidstrap.Enums.CursorType cursor)
        {
            var border = new System.Windows.Controls.Border
            {
                BorderBrush = new SolidColorBrush(Colors.Gray),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(5),
                Padding = new Thickness(10),
                Background = new SolidColorBrush(Colors.Transparent),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            var stackPanel = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal
            };

            // Load cursor image for preview
            var image = new System.Windows.Controls.Image
            {
                Width = 32,
                Height = 32,
                Margin = new Thickness(0, 0, 10, 0)
            };

            try
            {
                var imagePath = GetCursorImagePath(cursor);
                if (!string.IsNullOrEmpty(imagePath))
                {
                    var uri = new Uri($"pack://application:,,,/Resources/Mods/{imagePath}");
                    image.Source = new BitmapImage(uri);
                }
            }
            catch
            {
                // Use default image if cursor image can't be loaded
                image.Source = null;
            }

            var nameLabel = new System.Windows.Controls.TextBlock
            {
                Text = GetCursorDisplayName(cursor),
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 14
            };

            stackPanel.Children.Add(image);
            stackPanel.Children.Add(nameLabel);
            border.Child = stackPanel;

            border.MouseLeftButtonUp += (s, e) =>
            {
                SelectedCursor = cursor;
                DialogResult = true;
                Close();
            };

            border.MouseEnter += (s, e) =>
            {
                border.Background = new SolidColorBrush(Color.FromArgb(50, 100, 149, 237));
            };

            border.MouseLeave += (s, e) =>
            {
                border.Background = new SolidColorBrush(Colors.Transparent);
            };

            return border;
        }

        private string GetCursorImagePath(Voidstrap.Enums.CursorType cursor)
        {
            return cursor switch
            {
                Voidstrap.Enums.CursorType.FPSCursor => "Cursor/FPSCursor/ArrowCursor.png",
                Voidstrap.Enums.CursorType.CleanCursor => "Cursor/CleanCursor/ArrowCursor.png",
                Voidstrap.Enums.CursorType.DotCursor => "Cursor/DotCursor/ArrowCursor.png",
                Voidstrap.Enums.CursorType.StoofsCursor => "Cursor/StoofsCursor/ArrowCursor.png",
                Voidstrap.Enums.CursorType.From2006 => "Cursor/From2006/ArrowCursor.png",
                Voidstrap.Enums.CursorType.From2013 => "Cursor/From2013/ArrowCursor.png",
                Voidstrap.Enums.CursorType.WhiteDotCursor => "Cursor/WhiteDotCursor/ArrowCursor.png",
                Voidstrap.Enums.CursorType.VerySmallWhiteDot => "Cursor/VerySmallWhiteDot/ArrowCursor.png",
                _ => string.Empty
            };
        }

        private string GetCursorDisplayName(Voidstrap.Enums.CursorType cursor)
        {
            return cursor switch
            {
                Voidstrap.Enums.CursorType.Default => "Default",
                Voidstrap.Enums.CursorType.FPSCursor => "FPS Cursor (V1)",
                Voidstrap.Enums.CursorType.CleanCursor => "Clean Cursor",
                Voidstrap.Enums.CursorType.DotCursor => "Dot Cursor",
                Voidstrap.Enums.CursorType.StoofsCursor => "Stoofs Cursor",
                Voidstrap.Enums.CursorType.From2006 => "2006 Legacy Cursor",
                Voidstrap.Enums.CursorType.From2013 => "2013 Legacy Cursor",
                Voidstrap.Enums.CursorType.WhiteDotCursor => "White Dot Cursor",
                Voidstrap.Enums.CursorType.VerySmallWhiteDot => "Very Small White Dot",
                _ => cursor.ToString()
            };
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}