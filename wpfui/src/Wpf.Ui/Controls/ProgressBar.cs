using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace Wpf.Ui.Controls
{
    /// <summary>
    /// Represents a ProgressBar with customizable corner radii for the control and the indicator.
    /// </summary>
    public sealed class ProgressBar : System.Windows.Controls.ProgressBar
    {
        /// <summary>
        /// Identifies the <see cref="CornerRadius"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(
            nameof(CornerRadius),
            typeof(CornerRadius),
            typeof(ProgressBar),
            new PropertyMetadata(new CornerRadius(4), null, CoerceCornerRadius));

        /// <summary>
        /// Identifies the <see cref="IndicatorCornerRadius"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty IndicatorCornerRadiusProperty = DependencyProperty.Register(
            nameof(IndicatorCornerRadius),
            typeof(CornerRadius),
            typeof(ProgressBar),
            new PropertyMetadata(new CornerRadius(4), null, CoerceCornerRadius));

        /// <summary>
        /// Gets or sets the corner radius of the progress bar's background.
        /// </summary>
        [Bindable(true)]
        [Category("Appearance")]
        [Description("The corner radius of the progress bar's background.")]
        public CornerRadius CornerRadius
        {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        /// <summary>
        /// Gets or sets the corner radius of the progress bar's indicator.
        /// </summary>
        [Bindable(true)]
        [Category("Appearance")]
        [Description("The corner radius of the progress bar's indicator.")]
        public CornerRadius IndicatorCornerRadius
        {
            get => (CornerRadius)GetValue(IndicatorCornerRadiusProperty);
            set => SetValue(IndicatorCornerRadiusProperty, value);
        }

        /// <summary>
        /// Ensures the CornerRadius values are not negative.
        /// </summary>
        private static object CoerceCornerRadius(DependencyObject d, object baseValue)
        {
            if (baseValue is CornerRadius radius)
            {
                return new CornerRadius(
                    Math.Max(0, radius.TopLeft),
                    Math.Max(0, radius.TopRight),
                    Math.Max(0, radius.BottomRight),
                    Math.Max(0, radius.BottomLeft));
            }
            return baseValue;
        }
    }
}
