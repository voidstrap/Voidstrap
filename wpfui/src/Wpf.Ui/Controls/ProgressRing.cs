// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// https://docs.microsoft.com/en-us/fluent-ui/web-components/components/progress-ring

using System.ComponentModel;
using System.Drawing;
using System.Windows;
using System.Windows.Controls.Primitives;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;

namespace Wpf.Ui.Controls;

/// <summary>
/// Rotating loading ring.
/// </summary>
[ToolboxItem(true)]
[ToolboxBitmap(typeof(ProgressRing), "ProgressRing.bmp")]
public class ProgressRing : RangeBase
{
    /// <summary>
    /// Property for <see cref="IsIndeterminate"/>.
    /// </summary>
    public static readonly DependencyProperty IsIndeterminateProperty = DependencyProperty.Register(
        nameof(IsIndeterminate),
        typeof(bool), typeof(ProgressRing),
        new PropertyMetadata(false));

    /// <summary>
    /// Property for <see cref="EngAngle"/>.
    /// </summary>
    public static readonly DependencyProperty EngAngleProperty = DependencyProperty.Register(nameof(EngAngle),
        typeof(double), typeof(ProgressRing),
        new PropertyMetadata(180.0d));

    /// <summary>
    /// Property for <see cref="IndeterminateAngle"/>.
    /// </summary>
    public static readonly DependencyProperty IndeterminateAngleProperty = DependencyProperty.Register(
        nameof(IndeterminateAngle),
        typeof(double), typeof(ProgressRing),
        new PropertyMetadata(180.0d));

    /// <summary>
    /// Property for <see cref="CoverRingStroke"/>.
    /// </summary>
    public static readonly DependencyProperty CoverRingStrokeProperty =
        DependencyProperty.RegisterAttached(
            nameof(CoverRingStroke),
            typeof(Brush),
            typeof(ProgressRing),
            new FrameworkPropertyMetadata(
                Brushes.Black,
                FrameworkPropertyMetadataOptions.AffectsRender |
                FrameworkPropertyMetadataOptions.SubPropertiesDoNotAffectRender |
                FrameworkPropertyMetadataOptions.Inherits));

    /// <summary>
    /// Property for <see cref="CoverRingVisibility"/>.
    /// </summary>
    public static readonly DependencyProperty CoverRingVisibilityProperty = DependencyProperty.Register(
        nameof(CoverRingVisibility),
        typeof(System.Windows.Visibility), typeof(ProgressRing),
        new PropertyMetadata(System.Windows.Visibility.Visible));

    /// <summary>
    /// Determines if <see cref="ProgressRing"/> shows actual values (<see langword="false"/>)
    /// or generic, continuous progress feedback (<see langword="true"/>).
    /// </summary>
    public bool IsIndeterminate
    {
        get => (bool)GetValue(IsIndeterminateProperty);
        set => SetValue(IsIndeterminateProperty, value);
    }

    /// <summary>
    /// Gets or sets the <see cref="Arc.EndAngle"/>.
    /// </summary>
    public double EngAngle
    {
        get => (double)GetValue(EngAngleProperty);
        set => SetValue(EngAngleProperty, value);
    }

    /// <summary>
    /// Gets the <see cref="Arc.EndAngle"/> when <see cref="IsIndeterminate"/> is <see langword="true"/>.
    /// </summary>
    public double IndeterminateAngle
    {
        get => (double)GetValue(IndeterminateAngleProperty);
        internal set => SetValue(IndeterminateAngleProperty, value);
    }

    /// <summary>
    /// Background ring fill.
    /// </summary>
    public Brush CoverRingStroke
    {
        get => (Brush)GetValue(CoverRingStrokeProperty);
        internal set => SetValue(CoverRingStrokeProperty, value);
    }

    /// <summary>
    /// Background ring visibility.
    /// </summary>
    public System.Windows.Visibility CoverRingVisibility
    {
        get => (System.Windows.Visibility)GetValue(CoverRingVisibilityProperty);
        internal set => SetValue(CoverRingVisibilityProperty, value);
    }

    public ProgressRing()
    {
        UpdateProgressAngle();
    }

    /// <summary>
    /// Re-draws <see cref="Arc.EndAngle"/> depending on <see cref="Progress"/>.
    /// </summary>
    protected void UpdateProgressAngle()
    {
        // (360 / 100) * percentage
        var endAngle = (360d / Maximum) * Value;

        if (endAngle >= 360)
            endAngle = 359;

        EngAngle = endAngle;
    }

    protected override void OnValueChanged(double oldValue, double newValue)
    {
        base.OnValueChanged(oldValue, newValue);
        UpdateProgressAngle();
    }

    protected override void OnMaximumChanged(double oldMaximum, double newMaximum)
    {
        base.OnMaximumChanged(oldMaximum, newMaximum);
        UpdateProgressAngle();
    }
}
