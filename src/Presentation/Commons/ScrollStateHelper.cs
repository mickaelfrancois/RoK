using Microsoft.UI.Xaml.Controls;

namespace Rok.Commons;

public static class ScrollStateHelper
{
    // Bounds the number of layout passes we wait for the scrollable extent to materialize
    // before giving up, so a one-shot LayoutUpdated handler never stays attached indefinitely.
    private const int KMaxRestoreAttempts = 60;

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
        double target = _verticalScrollOffset;

        if (target <= 0)
            return;

        ScrollViewer? scrollViewer = GetScrollViewer(dependencyObject);

        if (scrollViewer == null)
            return;

        // When the page reloads, the list has just been given its ItemsSource and has not yet
        // realized/measured its containers, so ScrollableHeight is still 0 and an immediate
        // ChangeView clamps back to the top. Wait for the extent to reach the saved offset, then
        // restore once (or clamp to whatever extent exists if the content genuinely shrank).
        if (TryApplyOffset(scrollViewer, target, force: false))
            return;

        int attempts = 0;

        void OnLayoutUpdated(object? sender, object e)
        {
            attempts++;

            bool lastAttempt = attempts >= KMaxRestoreAttempts;

            if (TryApplyOffset(scrollViewer, target, force: lastAttempt) || lastAttempt)
                scrollViewer.LayoutUpdated -= OnLayoutUpdated;
        }

        scrollViewer.LayoutUpdated += OnLayoutUpdated;
    }


    private static bool TryApplyOffset(ScrollViewer scrollViewer, double target, bool force)
    {
        if (scrollViewer.ScrollableHeight <= 0)
            return false;

        // Keep waiting while the extent is still growing toward the saved offset, unless this is
        // the final attempt — then restore as close as the current extent allows.
        if (!force && scrollViewer.ScrollableHeight < target)
            return false;

        double clamped = System.Math.Min(target, scrollViewer.ScrollableHeight);
        scrollViewer.ChangeView(null, clamped, null, true);

        return true;
    }


    public static void SaveScrollOffset(DependencyObject dependencyObject)
    {
        ScrollViewer? scrollViewer = GetScrollViewer(dependencyObject);

        if (scrollViewer != null)
            _verticalScrollOffset = scrollViewer.VerticalOffset;
    }
}
