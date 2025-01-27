// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Wpf.Ui.Hardware;

namespace Wpf.Ui.Animations
{
    /// <summary>
    /// Provides tools for <see cref="FrameworkElement"/> animation.
    /// </summary>
    public static class Transitions
    {
        private const double DecelerationRatio = 0.7;
        private const int MinDuration = 10;
        private const int MaxDuration = 10000;

        /// <summary>
        /// Attempts to apply an animation effect while adding content to the frame.
        /// </summary>
        /// <param name="element">Currently rendered element.</param>
        /// <param name="type">Selected transition type.</param>
        /// <param name="duration">Transition duration in milliseconds.</param>
        public static bool ApplyTransition(object element, TransitionType type, int duration)
        {
            // Validate input parameters
            if (type == TransitionType.None || duration < MinDuration || element is not FrameworkElement frameworkElement)
                return false;

            // Cap the duration to the maximum allowed value
            duration = Math.Min(duration, MaxDuration);

            // Disable transitions for non-accelerated devices
            if (!HardwareAcceleration.IsSupported(RenderingTier.PartialAcceleration))
                return false;

            var timespanDuration = new Duration(TimeSpan.FromMilliseconds(duration));

            // Select and apply the appropriate transition
            switch (type)
            {
                case TransitionType.FadeIn:
                    ApplyFadeIn(frameworkElement, timespanDuration);
                    break;
                case TransitionType.FadeInWithSlide:
                    ApplyFadeInWithSlide(frameworkElement, timespanDuration);
                    break;
                case TransitionType.SlideBottom:
                    ApplySlide(frameworkElement, timespanDuration, TranslateTransform.YProperty, 30);
                    break;
                case TransitionType.SlideRight:
                    ApplySlide(frameworkElement, timespanDuration, TranslateTransform.XProperty, 50);
                    break;
                case TransitionType.SlideLeft:
                    ApplySlide(frameworkElement, timespanDuration, TranslateTransform.XProperty, -50);
                    break;
            }

            return true;
        }

        private static void ApplyFadeIn(FrameworkElement element, Duration duration)
        {
            var fadeAnimation = new DoubleAnimation
            {
                Duration = duration,
                DecelerationRatio = DecelerationRatio,
                From = 0.0,
                To = 1.0
            };

            element.BeginAnimation(UIElement.OpacityProperty, fadeAnimation);
        }

        private static void ApplyFadeInWithSlide(FrameworkElement element, Duration duration)
        {
            EnsureTranslateTransform(element);

            // Apply slide animation
            var slideAnimation = new DoubleAnimation
            {
                Duration = duration,
                DecelerationRatio = DecelerationRatio,
                From = 30,
                To = 0
            };
            element.RenderTransform.BeginAnimation(TranslateTransform.YProperty, slideAnimation);

            // Apply fade animation
            var fadeAnimation = new DoubleAnimation
            {
                Duration = duration,
                DecelerationRatio = DecelerationRatio,
                From = 0.0,
                To = 1.0
            };
            element.BeginAnimation(UIElement.OpacityProperty, fadeAnimation);
        }

        private static void ApplySlide(FrameworkElement element, Duration duration, DependencyProperty property, double from)
        {
            EnsureTranslateTransform(element);

            var slideAnimation = new DoubleAnimation
            {
                Duration = duration,
                DecelerationRatio = DecelerationRatio,
                From = from,
                To = 0
            };

            element.RenderTransform.BeginAnimation(property, slideAnimation);
        }

        private static void EnsureTranslateTransform(FrameworkElement element)
        {
            if (element.RenderTransform is not TranslateTransform)
                element.RenderTransform = new TranslateTransform(0, 0);

            if (!element.RenderTransformOrigin.Equals(new Point(0.5, 0.5)))
                element.RenderTransformOrigin = new Point(0.5, 0.5);
        }
    }
}
