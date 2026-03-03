using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace Voidstrap.Helpers
{
    public static class SmoothScrollBehavior
    {
        private static readonly DependencyProperty VerticalOffsetProxyProperty =
            DependencyProperty.RegisterAttached(
                "VerticalOffsetProxy",
                typeof(double),
                typeof(SmoothScrollBehavior),
                new PropertyMetadata(0.0, OnVerticalOffsetProxyChanged));

        private static readonly DependencyProperty TargetOffsetProperty =
            DependencyProperty.RegisterAttached(
                "TargetOffset",
                typeof(double),
                typeof(SmoothScrollBehavior),
                new PropertyMetadata(0.0));

        static SmoothScrollBehavior()
        {
            EventManager.RegisterClassHandler(
                typeof(ScrollViewer),
                UIElement.PreviewMouseWheelEvent,
                new MouseWheelEventHandler(OnPreviewMouseWheel),
                true);
        }

        private static void OnVerticalOffsetProxyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ScrollViewer sv)
            {
                sv.ScrollToVerticalOffset((double)e.NewValue);
            }
        }

        private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is not ScrollViewer sv) return;
            if (IsInsideDataGrid(sv)) return;
            if (sv.ScrollableHeight <= 0) return;

            e.Handled = true;
            double delta = -e.Delta * 0.4;
            double target = Math.Max(0, Math.Min((double)sv.GetValue(TargetOffsetProperty) + delta, sv.ScrollableHeight));
            sv.SetValue(TargetOffsetProperty, target);

            var animation = new DoubleAnimation
            {
                From = (double)sv.GetValue(VerticalOffsetProxyProperty),
                To = target,
                Duration = TimeSpan.FromMilliseconds(250),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            animation.FillBehavior = FillBehavior.Stop;
            animation.Completed += (s, a) => sv.SetValue(VerticalOffsetProxyProperty, target);

            sv.BeginAnimation(VerticalOffsetProxyProperty, animation);

            if (Math.Abs(e.Delta) > 30)
            {
                if (!(sv.Effect is BlurEffect blur))
                {
                    blur = new BlurEffect { Radius = 0 };
                    sv.Effect = blur;
                }

                var blurAnim = new DoubleAnimation
                {
                    From = 2,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(150),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                    FillBehavior = FillBehavior.Stop
                };
                blur.BeginAnimation(BlurEffect.RadiusProperty, blurAnim);
            }
        }

        private static bool IsInsideDataGrid(DependencyObject obj)
        {
            while (obj != null)
            {
                if (obj is DataGrid) return true;
                obj = VisualTreeHelper.GetParent(obj);
            }
            return false;
        }
    }
}