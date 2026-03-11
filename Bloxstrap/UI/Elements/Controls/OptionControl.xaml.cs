using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Threading;

namespace Voidstrap.UI.Elements.Controls
{
    [ContentProperty(nameof(InnerContent))]
    public partial class OptionControl : UserControl
    {
        public OptionControl()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty HeaderProperty =
            DependencyProperty.Register(nameof(Header), typeof(string), typeof(OptionControl));

        public string Header
        {
            get => (string)GetValue(HeaderProperty);
            set => SetValue(HeaderProperty, value);
        }

        public static readonly DependencyProperty DescriptionProperty =
            DependencyProperty.Register(nameof(Description), typeof(string), typeof(OptionControl));

        public string Description
        {
            get => (string)GetValue(DescriptionProperty);
            set => SetValue(DescriptionProperty, value);
        }

        public static readonly DependencyProperty HelpLinkProperty =
            DependencyProperty.Register(nameof(HelpLink), typeof(string), typeof(OptionControl));

        public string HelpLink
        {
            get => (string)GetValue(HelpLinkProperty);
            set => SetValue(HelpLinkProperty, value);
        }

        public static readonly DependencyProperty InnerContentProperty =
            DependencyProperty.Register(nameof(InnerContent), typeof(object), typeof(OptionControl));

        public object InnerContent
        {
            get => GetValue(InnerContentProperty);
            set
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
                {
                    SetValue(InnerContentProperty, value);
                }));
            }
        }
    }
}