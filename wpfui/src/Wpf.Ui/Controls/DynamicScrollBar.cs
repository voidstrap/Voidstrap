// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT License was not distributed with this file, you can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.ComponentModel;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Wpf.Ui.Common;

namespace Wpf.Ui.Controls;

/// <summary>
/// A custom <see cref="System.Windows.Controls.Primitives.ScrollBar"/> that detects user interaction and scrolling activity.
/// </summary>
[ToolboxItem(true)]
[ToolboxBitmap(typeof(DynamicScrollBar), "DynamicScrollBar.bmp")]
public class DynamicScrollBar : System.Windows.Controls.Primitives.ScrollBar
{
    private bool _isScrolling;
    private bool _isInteracted;
    private readonly EventIdentifier _interactionTracker = new();

    #region Dependency Properties

    public static readonly DependencyProperty IsScrollingProperty =
        DependencyProperty.Register(
            nameof(IsScrolling),
            typeof(bool),
            typeof(DynamicScrollBar),
            new PropertyMetadata(false, OnIsScrollingChanged));

    public static readonly DependencyProperty IsInteractedProperty =
        DependencyProperty.Register(
            nameof(IsInteracted),
            typeof(bool),
            typeof(DynamicScrollBar),
            new PropertyMetadata(false, OnIsInteractedChanged));

    public static readonly DependencyProperty TimeoutProperty =
        DependencyProperty.Register(
            nameof(Timeout),
            typeof(int),
            typeof(DynamicScrollBar),
            new PropertyMetadata(1000));

    #endregion

    #region Properties

    /// <summary>
    /// Indicates whether the user is currently scrolling.
    /// </summary>
    public bool IsScrolling
    {
        get => (bool)GetValue(IsScrollingProperty);
        set => SetValue(IsScrollingProperty, value);
    }

    /// <summary>
    /// Indicates whether the scrollbar is currently being interacted with (mouse over or scrolling).
    /// </summary>
    public bool IsInteracted
    {
        get => (bool)GetValue(IsInteractedProperty);
        set
        {
            if (_isInteracted != value)
                SetValue(IsInteractedProperty, value);
        }
    }

    /// <summary>
    /// Delay in milliseconds before the scrollbar hides after interaction ends.
    /// </summary>
    public int Timeout
    {
        get => (int)GetValue(TimeoutProperty);
        set => SetValue(TimeoutProperty, value);
    }

    #endregion

    #region Event Handlers

    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        _ = UpdateInteractionStateAsync();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _ = UpdateInteractionStateAsync();
    }

    #endregion

    #region Interaction Logic

    private async Task UpdateInteractionStateAsync()
    {
        var interactionEvent = _interactionTracker.GetNext();
        bool shouldBeInteracted = IsMouseOver || _isScrolling;

        if (shouldBeInteracted == _isInteracted)
            return;

        if (!shouldBeInteracted)
            await Task.Delay(Timeout);

        if (!_interactionTracker.IsEqual(interactionEvent))
            return;

        IsInteracted = shouldBeInteracted;
    }

    private static void OnIsScrollingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DynamicScrollBar scrollbar)
        {
            bool newValue = (bool)e.NewValue;
            if (scrollbar._isScrolling != newValue)
            {
                scrollbar._isScrolling = newValue;
                _ = scrollbar.UpdateInteractionStateAsync();
            }
        }
    }

    private static void OnIsInteractedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DynamicScrollBar scrollbar)
        {
            bool newValue = (bool)e.NewValue;
            if (scrollbar._isInteracted != newValue)
            {
                scrollbar._isInteracted = newValue;
                _ = scrollbar.UpdateInteractionStateAsync();
            }
        }
    }

    #endregion
}
