using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using Wpf.Ui.Hardware;

namespace Wpf.Ui.Animations
{
    internal static class Easings
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Smooth(double t)
        {
            t = t * t * (3 - 2 * t);
            return t + Math.Sin(t * Math.PI) * 0.05;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Fade(double t)
        {
            return t * t * (3 - 2 * t);
        }
    }

    internal sealed class FrameAnimator
    {
        private readonly Action<double> _update;
        private readonly Action? _completed;
        private readonly double _durationMs;

        private readonly Stopwatch _watch = new();
        private bool _running;

        public FrameAnimator(double durationMs, Action<double> update, Action? completed = null)
        {
            _durationMs = durationMs;
            _update = update;
            _completed = completed;
        }

        public void Start()
        {
            if (_running)
                return;

            _running = true;
            _watch.Restart();
            CompositionTarget.Rendering += OnRendering;
        }

        private void OnRendering(object? sender, EventArgs e)
        {
            var elapsed = _watch.Elapsed.TotalMilliseconds;
            var t = elapsed / _durationMs;

            if (t >= 1.0)
            {
                _update(1.0);
                Stop();
                return;
            }

            _update(t);
        }

        private void Stop()
        {
            if (!_running)
                return;

            _running = false;
            _watch.Stop();
            CompositionTarget.Rendering -= OnRendering;
            _completed?.Invoke();
        }
    }

    public static class Transitions
    {
        private const int MinDuration = 260;
        private const int MaxDuration = 1250;

        public static bool ApplyTransition(
            object element,
            TransitionType type,
            int duration)
        {
            if (type == TransitionType.None ||
                element is not FrameworkElement fe ||
                !HardwareAcceleration.IsSupported(
                    HardwareAcceleration.RenderingTier.PartialAcceleration))
            {
                return false;
            }

            duration = Math.Clamp(duration, MinDuration, MaxDuration);

            switch (type)
            {
                case TransitionType.FadeIn:
                    FadeIn(fe, duration);
                    break;

                case TransitionType.SlideBottom:
                    Slide(fe, 0, 40, duration, false);
                    break;

                case TransitionType.SlideRight:
                    Slide(fe, 50, 0, duration, false);
                    break;

                case TransitionType.SlideLeft:
                    Slide(fe, -50, 0, duration, false);
                    break;

                case TransitionType.FadeInWithSlide:
                    Slide(fe, 0, 40, duration, true);
                    break;

                case TransitionType.FadeInWithSlideRight:
                    Slide(fe, 50, 0, duration, true);
                    break;

                default:
                    return false;
            }

            return true;
        }

        private static void FadeIn(FrameworkElement element, int durationMs)
        {
            element.Opacity = 0;

            var animator = new FrameAnimator(
                durationMs,
                t => element.Opacity = Easings.Fade(t),
                () => element.Opacity = 1);

            animator.Start();
        }


        private static void Slide(
            FrameworkElement element,
            double offsetX,
            double offsetY,
            int durationMs,
            bool fade)
        {
            if (element.RenderTransform is not TranslateTransform transform)
            {
                transform = new TranslateTransform();
                element.RenderTransform = transform;
            }

            element.RenderTransformOrigin = new Point(0.5, 0.5);

            if (fade)
                element.Opacity = 0;

            var animator = new FrameAnimator(
                durationMs,
                t =>
                {
                    var eased = Easings.Smooth(t);

                    transform.X = Lerp(offsetX, 0, eased);
                    transform.Y = Lerp(offsetY, 0, eased);

                    if (fade)
                        element.Opacity = Easings.Fade(t);
                },
                () =>
                {
                    transform.X = 0;
                    transform.Y = 0;
                    element.Opacity = 1;
                    element.CacheMode = null;
                });

            animator.Start();
        }


        private static double Lerp(double from, double to, double t)
            => from + (to - from) * t;
    }
}
