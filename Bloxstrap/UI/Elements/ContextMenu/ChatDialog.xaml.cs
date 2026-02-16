using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Voidstrap.UI.Chat;
using Voidstrap.UI.Elements.Base;
using static Voidstrap.UI.Chat.DiscordChatViewModel;

namespace Voidstrap.UI.Elements.ContextMenu
{
    public partial class DiscordChatWindow : WpfUiWindow
    {
        public DiscordChatWindow()
        {
            InitializeComponent();
            DataContext = new DiscordChatViewModel();
            DataContextChanged += DiscordChatWindow_DataContextChanged;
        }

        private async void Emoji_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox cb && cb.SelectedItem != null)
            {
                if (cb.DataContext is ChatMessage msg &&
                    DataContext is DiscordChatViewModel vm &&
                    vm.SelectedTab != null)
                {
                    string emoji = cb.SelectedItem.ToString();

                    await vm.SelectedTab.ReactToMessage(msg, emoji);
                    cb.SelectedIndex = -1;
                }
            }
        }

        private void DiscordChatWindow_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (DataContext is DiscordChatViewModel vm)
            {
                vm.PropertyChanged += (s, args) =>
                {
                    if (args.PropertyName == nameof(DiscordChatViewModel.SelectedTab))
                    {
                        AttachAutoScroll(vm.SelectedTab);
                    }
                };

                AttachAutoScroll(vm.SelectedTab);
            }
        }

        private void AttachAutoScroll(DiscordChatViewModel.ChatTab tab)
        {
            if (tab == null) return;

            tab.Messages.CollectionChanged += (s, e) =>
            {
                if (e.Action == NotifyCollectionChangedAction.Add)
                {
                    var scrollViewer = FindScrollViewer(ChatTabControl);
                    scrollViewer?.ScrollToEnd();
                }
            };
        }

        private ScrollViewer FindScrollViewer(DependencyObject dep)
        {
            if (dep is ScrollViewer sv) return sv;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(dep); i++)
            {
                var child = VisualTreeHelper.GetChild(dep, i);
                var result = FindScrollViewer(child);
                if (result != null) return result;
            }

            return null;
        }

        private void Image_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Image img && img.Source != null)
            {
                var window = new Window
                {
                    Title = "Image Preview",
                    Content = new ScrollViewer
                    {
                        Content = new Image
                        {
                            Source = img.Source,
                            Stretch = System.Windows.Media.Stretch.Uniform
                        }
                    },
                    Width = 800,
                    Height = 600,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this
                };

                window.ShowDialog();
            }
        }
    }
}