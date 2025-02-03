using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Wpf.Ui.Hardware;

namespace Wpf.Ui.Animations
{
    public static class Transitions
    {
        private const double SmoothEasing = 0.85;

        public static bool ApplyTransition(object element, TransitionType type, int duration)
        {
            if (type == TransitionType.None || duration < 10)
                return false;

            duration = Math.Min(duration, 10000);

            if (!HardwareAcceleration.IsSupported(RenderingTier.PartialAcceleration))
                return false;

            if (element is not FrameworkElement frameworkElement)
                return false;

            var timespanDuration = new Duration(TimeSpan.FromMilliseconds(duration));

            switch (type)
            {
                case TransitionType.FadeIn:
                    ApplyOpacityTransition(frameworkElement, timespanDuration, 0.0, 1.0);
                    break;
                case TransitionType.FadeInWithSlide:
                    ApplySlideTransition(frameworkElement, timespanDuration, 40, 0);
                    ApplyOpacityTransition(frameworkElement, timespanDuration, 0.0, 1.0);
                    break;
                case TransitionType.SlideBottom:
                    ApplySlideTransition(frameworkElement, timespanDuration, 50, 0);
                    break;
                case TransitionType.SlideRight:
                    ApplySlideTransition(frameworkElement, timespanDuration, 60, 0, isHorizontal: true);
                    break;
                case TransitionType.SlideLeft:
                    ApplySlideTransition(frameworkElement, timespanDuration, -60, 0, isHorizontal: true);
                    break;
            }

            return true;
        }

        private static void ApplySlideTransition(FrameworkElement element, Duration duration, double from, double to, bool isHorizontal = false)
        {
            if (element.RenderTransform is not TranslateTransform)
                element.RenderTransform = new TranslateTransform();

            var translateAnimation = new DoubleAnimation
            {
                Duration = duration,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                From = from,
                To = to,
            };

            var property = isHorizontal ? TranslateTransform.XProperty : TranslateTransform.YProperty;
            element.RenderTransform.BeginAnimation(property, translateAnimation);
        }

        private static void ApplyOpacityTransition(FrameworkElement element, Duration duration, double from, double to)
        {
            var opacityAnimation = new DoubleAnimation
            {
                Duration = duration,
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                From = from,
                To = to,
            };
            element.BeginAnimation(UIElement.OpacityProperty, opacityAnimation);
        }
    }
}
