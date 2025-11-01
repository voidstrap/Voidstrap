using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Wpf.Ui.Hardware;

namespace Wpf.Ui.Animations
{
    public static class Transitions
    {
        private const int MaxDuration = 1400;
        private const int MinDuration = 150;
        public static bool ApplyTransition(object element, TransitionType type, int duration)
        {
            if (type == TransitionType.None ||
                duration < MinDuration ||
                !HardwareAcceleration.IsSupported(HardwareAcceleration.RenderingTier.PartialAcceleration) ||
                element is not FrameworkElement frameworkElement)
            {
                return false;
            }
            duration = Math.Clamp(duration, 400, MaxDuration);
            var animationDuration = new Duration(TimeSpan.FromMilliseconds(duration));

            RenderOptions.SetBitmapScalingMode(frameworkElement, BitmapScalingMode.Fant);

            switch (type)
            {
                case TransitionType.FadeIn:
                    ApplyFade(frameworkElement, animationDuration);
                    break;

                case TransitionType.FadeInWithSlide:
                    ApplySlide(frameworkElement, animationDuration, 0, 60, fade: true);
                    break;

                case TransitionType.SlideBottom:
                    ApplySlide(frameworkElement, animationDuration, 0, 60);
                    break;

                case TransitionType.SlideRight:
                    ApplySlide(frameworkElement, animationDuration, 80, 0);
                    break;

                case TransitionType.SlideLeft:
                    ApplySlide(frameworkElement, animationDuration, -80, 0);
                    break;

                default:
                    return false;
            }

            return true;
        }

        private static void ApplyFade(FrameworkElement element, Duration duration)
        {
            element.Opacity = 0;
            var fadeIn = new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                Duration = duration,
                EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut }
            };

            element.BeginAnimation(UIElement.OpacityProperty, fadeIn);
        }

        private static void ApplySlide(FrameworkElement element, Duration duration, double offsetX, double offsetY, bool fade = false)
        {
            if (element.RenderTransform is not TranslateTransform)
                element.RenderTransform = new TranslateTransform();

            element.RenderTransformOrigin = new Point(0.5, 0.5);
            var easing = new QuinticEase { EasingMode = EasingMode.EaseOut };

            var storyboard = new Storyboard();

            if (offsetX != 0)
            {
                var animX = new DoubleAnimation
                {
                    From = offsetX,
                    To = 0,
                    Duration = duration,
                    EasingFunction = easing
                };
                Storyboard.SetTarget(animX, element);
                Storyboard.SetTargetProperty(animX, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));
                storyboard.Children.Add(animX);
            }

            if (offsetY != 0)
            {
                var animY = new DoubleAnimation
                {
                    From = offsetY,
                    To = 0,
                    Duration = duration,
                    EasingFunction = easing
                };
                Storyboard.SetTarget(animY, element);
                Storyboard.SetTargetProperty(animY, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.Y)"));
                storyboard.Children.Add(animY);
            }

            if (fade)
            {
                element.Opacity = 0;
                var fadeIn = new DoubleAnimation
                {
                    From = 0.0,
                    To = 1.0,
                    Duration = duration,
                    EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut }
                };
                Storyboard.SetTarget(fadeIn, element);
                Storyboard.SetTargetProperty(fadeIn, new PropertyPath("Opacity"));
                storyboard.Children.Add(fadeIn);
            }

            storyboard.Begin();
        }
    }
}
