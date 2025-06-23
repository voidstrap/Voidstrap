using System;
using System.ComponentModel;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Wpf.Ui.Controls
{
    /// <summary>
    /// A ScrollViewer that exposes events and properties to track dynamic user scrolling states (vertical and horizontal).
    /// The scrolling state resets after a configurable timeout when no further scrolling occurs.
    /// </summary>
    [ToolboxItem(true)]
    [ToolboxBitmap(typeof(DynamicScrollViewer), "DynamicScrollViewer.bmp")]
    [DefaultEvent("ScrollChangedEvent")]
    public class DynamicScrollViewer : ScrollViewer
    {
        private CancellationTokenSource? _verticalCts;
        private CancellationTokenSource? _horizontalCts;

        private int _timeout = 1200;
        private double _minimalChange = 40d;

        public static readonly DependencyProperty IsScrollingVerticallyProperty = DependencyProperty.Register(
            nameof(IsScrollingVertically),
            typeof(bool), typeof(DynamicScrollViewer),
            new PropertyMetadata(false, OnIsScrollingVerticallyChanged));

        public static readonly DependencyProperty IsScrollingHorizontallyProperty = DependencyProperty.Register(
            nameof(IsScrollingHorizontally),
            typeof(bool), typeof(DynamicScrollViewer),
            new PropertyMetadata(false, OnIsScrollingHorizontallyChanged));

        public static readonly DependencyProperty MinimalChangeProperty = DependencyProperty.Register(
            nameof(MinimalChange),
            typeof(double), typeof(DynamicScrollViewer),
            new PropertyMetadata(40d, OnMinimalChangeChanged));

        public static readonly DependencyProperty TimeoutProperty = DependencyProperty.Register(
            nameof(Timeout),
            typeof(int), typeof(DynamicScrollViewer),
            new PropertyMetadata(1200, OnTimeoutChanged));

        /// <summary>
        /// True if the user has been scrolling vertically recently.
        /// </summary>
        public bool IsScrollingVertically
        {
            get => (bool)GetValue(IsScrollingVerticallyProperty);
            set => SetValue(IsScrollingVerticallyProperty, value);
        }

        /// <summary>
        /// True if the user has been scrolling horizontally recently.
        /// </summary>
        public bool IsScrollingHorizontally
        {
            get => (bool)GetValue(IsScrollingHorizontallyProperty);
            set => SetValue(IsScrollingHorizontallyProperty, value);
        }

        /// <summary>
        /// The minimal scroll delta that triggers scrolling state updates.
        /// </summary>
        public double MinimalChange
        {
            get => _minimalChange;
            set => SetValue(MinimalChangeProperty, value);
        }

        /// <summary>
        /// The timeout in milliseconds after which scrolling states are reset.
        /// </summary>
        public int Timeout
        {
            get => _timeout;
            set => SetValue(TimeoutProperty, value);
        }

        protected override void OnScrollChanged(ScrollChangedEventArgs e)
        {
            base.OnScrollChanged(e);

            if (Math.Abs(e.VerticalChange) >= _minimalChange)
                UpdateScrollingStateAsync(isVertical: true);

            if (Math.Abs(e.HorizontalChange) >= _minimalChange)
                UpdateScrollingStateAsync(isVertical: false);
        }

        private async void UpdateScrollingStateAsync(bool isVertical)
        {
            CancellationTokenSource? cts;

            if (isVertical)
            {
                _verticalCts?.Cancel();
                _verticalCts = new CancellationTokenSource();
                cts = _verticalCts;
                if (!IsScrollingVertically)
                    IsScrollingVertically = true;
            }
            else
            {
                _horizontalCts?.Cancel();
                _horizontalCts = new CancellationTokenSource();
                cts = _horizontalCts;
                if (!IsScrollingHorizontally)
                    IsScrollingHorizontally = true;
            }

            try
            {
                await Task.Delay(Math.Min(_timeout, 10000), cts.Token);

                if (!cts.Token.IsCancellationRequested)
                {
                    if (isVertical)
                        IsScrollingVertically = false;
                    else
                        IsScrollingHorizontally = false;
                }
            }
            catch (TaskCanceledException)
            {
                // Ignore cancellation; a new scroll event restarted the timeout.
            }
        }

        private static void OnIsScrollingVerticallyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DynamicScrollViewer scroll)
                scroll._scrollingVertically = (bool)e.NewValue;
        }

        private static void OnIsScrollingHorizontallyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DynamicScrollViewer scroll)
                scroll._scrollingHorizontally = (bool)e.NewValue;
        }

        private static void OnMinimalChangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DynamicScrollViewer scroll)
                scroll._minimalChange = (double)e.NewValue;
        }

        private static void OnTimeoutChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DynamicScrollViewer scroll)
                scroll._timeout = (int)e.NewValue;
        }

        // Backing fields for DependencyProperty sync
        private bool _scrollingVertically;
        private bool _scrollingHorizontally;
    }
}
