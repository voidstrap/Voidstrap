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

            // Limit duration for performance
            duration = Math.Min(duration, 10000);

            // Check for hardware acceleration support
            if (!HardwareAcceleration.IsSupported(RenderingTier.PartialAcceleration))
                return false;

            // Ensure element is a valid FrameworkElement
            if (element is not FrameworkElement frameworkElement)
                return false;

            var timespanDuration = new Duration(TimeSpan.FromMilliseconds(duration));

            // Apply transition based on the type
            return type switch
            {
                TransitionType.FadeIn => ApplyFadeIn(frameworkElement, timespanDuration),
                TransitionType.FadeInWithSlide => ApplyFadeInWithSlide(frameworkElement, timespanDuration),
                TransitionType.SlideBottom => ApplySlide(frameworkElement, timespanDuration, 50, 0),
                TransitionType.SlideRight => ApplySlide(frameworkElement, timespanDuration, 60, 0, true),
                TransitionType.SlideLeft => ApplySlide(frameworkElement, timespanDuration, -60, 0, true),
                _ => false,
            };
        }

        private static bool ApplyFadeIn(FrameworkElement element, Duration duration)
        {
            return ApplyOpacityTransition(element, duration, 0.0, 1.0);
        }

        private static bool ApplyFadeInWithSlide(FrameworkElement element, Duration duration)
        {
            ApplySlideTransition(element, duration, 40, 0);
            return ApplyOpacityTransition(element, duration, 0.0, 1.0);
        }

        private static bool ApplySlide(FrameworkElement element, Duration duration, double from, double to, bool isHorizontal = false)
        {
            ApplySlideTransition(element, duration, from, to, isHorizontal);
            return true;
        }

        private static void ApplySlideTransition(FrameworkElement element, Duration duration, double from, double to, bool isHorizontal = false)
        {
            // Ensure RenderTransform is set correctly
            if (element.RenderTransform is not TranslateTransform translateTransform)
            {
                translateTransform = new TranslateTransform();
                element.RenderTransform = translateTransform;
            }

            // Create and apply animation with cubic easing
            var translateAnimation = CreateDoubleAnimation(duration, from, to);
            var property = isHorizontal ? TranslateTransform.XProperty : TranslateTransform.YProperty;
            translateTransform.BeginAnimation(property, translateAnimation);
        }

        private static bool ApplyOpacityTransition(FrameworkElement element, Duration duration, double from, double to)
        {
            // Create and apply opacity animation with quadratic easing
            var opacityAnimation = CreateDoubleAnimation(duration, from, to, new QuadraticEase { EasingMode = EasingMode.EaseOut });
            element.BeginAnimation(UIElement.OpacityProperty, opacityAnimation);
            return true;
        }

        private static DoubleAnimation CreateDoubleAnimation(Duration duration, double from, double to, IEasingFunction easingFunction = null)
        {
            return new DoubleAnimation
            {
                Duration = duration,
                From = from,
                To = to,
                EasingFunction = easingFunction ?? new CubicEase { EasingMode = EasingMode.EaseOut }
            };
        }
    }
}
