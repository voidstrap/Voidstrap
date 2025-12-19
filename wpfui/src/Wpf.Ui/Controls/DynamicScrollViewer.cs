using System;
using System.ComponentModel;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Wpf.Ui.Controls
{
    [ToolboxItem(true)]
    [ToolboxBitmap(typeof(DynamicScrollViewer), "DynamicScrollViewer.bmp")]
    public class DynamicScrollViewer : ScrollViewer
    {
        private CancellationTokenSource? _verticalCts;
        private CancellationTokenSource? _horizontalCts;

        private int _timeout = 900;
        private double _minimalChange = 8d;

        public static readonly DependencyProperty IsScrollingVerticallyProperty =
            DependencyProperty.Register(
                nameof(IsScrollingVertically),
                typeof(bool),
                typeof(DynamicScrollViewer),
                new PropertyMetadata(false));

        public static readonly DependencyProperty IsScrollingHorizontallyProperty =
            DependencyProperty.Register(
                nameof(IsScrollingHorizontally),
                typeof(bool),
                typeof(DynamicScrollViewer),
                new PropertyMetadata(false));

        public static readonly DependencyProperty MinimalChangeProperty =
            DependencyProperty.Register(
                nameof(MinimalChange),
                typeof(double),
                typeof(DynamicScrollViewer),
                new PropertyMetadata(8d, OnMinimalChangeChanged));

        public static readonly DependencyProperty TimeoutProperty =
            DependencyProperty.Register(
                nameof(Timeout),
                typeof(int),
                typeof(DynamicScrollViewer),
                new PropertyMetadata(900, OnTimeoutChanged));

        public bool IsScrollingVertically
        {
            get => (bool)GetValue(IsScrollingVerticallyProperty);
            private set => SetValue(IsScrollingVerticallyProperty, value);
        }

        public bool IsScrollingHorizontally
        {
            get => (bool)GetValue(IsScrollingHorizontallyProperty);
            private set => SetValue(IsScrollingHorizontallyProperty, value);
        }

        public double MinimalChange
        {
            get => _minimalChange;
            set => SetValue(MinimalChangeProperty, value);
        }

        public int Timeout
        {
            get => _timeout;
            set => SetValue(TimeoutProperty, value);
        }

        protected override void OnScrollChanged(ScrollChangedEventArgs e)
        {
            base.OnScrollChanged(e);

            if (Math.Abs(e.VerticalChange) >= _minimalChange)
                TriggerScrollState(isVertical: true);

            if (Math.Abs(e.HorizontalChange) >= _minimalChange)
                TriggerScrollState(isVertical: false);
        }

        private void TriggerScrollState(bool isVertical)
        {
            if (isVertical)
            {
                _verticalCts?.Cancel();
                _verticalCts = new CancellationTokenSource();

                if (!IsScrollingVertically)
                    IsScrollingVertically = true;

                _ = ResetScrollStateAsync(_verticalCts.Token, isVertical);
            }
            else
            {
                _horizontalCts?.Cancel();
                _horizontalCts = new CancellationTokenSource();

                if (!IsScrollingHorizontally)
                    IsScrollingHorizontally = true;

                _ = ResetScrollStateAsync(_horizontalCts.Token, isVertical);
            }
        }

        private async Task ResetScrollStateAsync(CancellationToken token, bool isVertical)
        {
            try
            {
                // Grace period: feels smoother than hard cutoff
                await Task.Delay(_timeout, token);
                await Task.Delay(120, token);

                if (token.IsCancellationRequested)
                    return;

                if (isVertical)
                    IsScrollingVertically = false;
                else
                    IsScrollingHorizontally = false;
            }
            catch (TaskCanceledException)
            {
                // Expected during continuous scrolling
            }
        }

        private static void OnMinimalChangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DynamicScrollViewer scroll)
                scroll._minimalChange = Math.Max(1d, (double)e.NewValue);
        }

        private static void OnTimeoutChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DynamicScrollViewer scroll)
                scroll._timeout = Math.Max(100, (int)e.NewValue);
        }
    }
}
