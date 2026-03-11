using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

public static class VisualTreeHelpers
{
    public static IEnumerable<T> FindVisualChildren<T>(this DependencyObject depObj) where T : DependencyObject
    {
        if (depObj == null) yield break;

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
            if (child is T t)
                yield return t;

            foreach (T childOfChild in child.FindVisualChildren<T>())
                yield return childOfChild;
        }
    }
}