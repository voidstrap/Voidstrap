using System.ComponentModel;
using System.Windows;

namespace Wpf.Ui.Controls;

public class ProgressBar : System.Windows.Controls.ProgressBar
{
    /// <summary>
    /// Property for <see cref="CornerRadius"/>.
    /// </summary>
    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(
        nameof(CornerRadius),
        typeof(CornerRadius), typeof(ProgressBar),
        new PropertyMetadata(new CornerRadius(4)));

    /// <summary>
    /// Property for <see cref="IndicatorCornerRadius"/>.
    /// </summary>
    public static readonly DependencyProperty IndicatorCornerRadiusProperty = DependencyProperty.Register(
        nameof(IndicatorCornerRadius),
        typeof(CornerRadius), typeof(ProgressBar),
        new PropertyMetadata(new CornerRadius(4)));

    [Bindable(true), Category("Appearance")]
    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    [Bindable(true), Category("Appearance")]
    public CornerRadius IndicatorCornerRadius
    {
        get => (CornerRadius)GetValue(IndicatorCornerRadiusProperty);
        set => SetValue(IndicatorCornerRadiusProperty, value);
    }
}
