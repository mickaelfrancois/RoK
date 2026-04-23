using Microsoft.UI.Xaml.Controls;

namespace Rok.Commons;

public static class ScrollStateHelper
{
    private static double _verticalScrollOffset = 0;

    private static ScrollViewer? GetScrollViewer(DependencyObject dependencyObject)
    {
        if (dependencyObject is ScrollViewer)
            return (ScrollViewer)dependencyObject;

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(dependencyObject); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(dependencyObject, i);
            ScrollViewer? result = GetScrollViewer(child);

            if (result != null)
                return result;
        }

        return null;
    }


    public static void RestoreScrollOffset(DependencyObject dependencyObject)
    {
        ScrollViewer? scrollViewer = GetScrollViewer(dependencyObject);

        if (scrollViewer != null && _verticalScrollOffset > 0)
            scrollViewer.ChangeView(null, _verticalScrollOffset, null, true);
    }


    public static void SaveScrollOffset(DependencyObject dependencyObject)
    {
        ScrollViewer? scrollViewer = GetScrollViewer(dependencyObject);

        if (scrollViewer != null)
            _verticalScrollOffset = scrollViewer.VerticalOffset;
    }
}
