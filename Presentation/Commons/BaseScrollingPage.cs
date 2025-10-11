using Microsoft.UI.Xaml.Controls;

namespace Rok.Commons;

public partial class BaseScrollingPage : IDisposable
{
    public const int KScrollHeaderStep = 2;

    private ScrollViewer? _scrollViewer;
    private bool disposedValue;
    private readonly Page _page;

    private readonly ListViewBase _container;

    private readonly RowDefinition _headerRow;

    private readonly ColumnDefinition _pictureColumn;

    private readonly VisualStateGroup? _stateGroup;


    public BaseScrollingPage(Page page, VisualStateGroup? stateGroup, ListViewBase container, RowDefinition headerRow, ColumnDefinition pictureColumn, ScrollViewer? scrollViewer = null)
    {
        _page = page;
        _stateGroup = stateGroup;
        _container = container;
        _pictureColumn = pictureColumn;
        _headerRow = headerRow;
        _scrollViewer = scrollViewer;

        _page.Loaded += Page_Loaded;

        if (_stateGroup != null)
            _stateGroup.CurrentStateChanging += VsGroupHeader_CurrentStateChanging;
    }


    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        if (_scrollViewer == null)
            _scrollViewer = GetScrollViewer(_container);

        if (_scrollViewer != null)
        {
            _scrollViewer.ViewChanging += Scroll_ViewChanging;
            ScrollHeader(_scrollViewer.VerticalOffset);
        }
    }


    private void ScrollHeader(double verticalOffset)
    {
        double headerRowHeight = _headerRow.MaxHeight;
        double newheight = headerRowHeight - (verticalOffset / KScrollHeaderStep);

        if (newheight >= _headerRow.MinHeight)
        {
            double gridPadding = headerRowHeight - verticalOffset;
            gridPadding = gridPadding < 0 ? 0 : gridPadding;

            _headerRow.Height = new GridLength(newheight);
            _pictureColumn.Width = new GridLength(newheight);
            _container.Padding = new Thickness(0, gridPadding, 0, 0);
        }
        else
        {
            _headerRow.Height = new GridLength(_headerRow.MinHeight);
            _pictureColumn.Width = new GridLength(_headerRow.MinHeight);
        }
    }


    private void Scroll_ViewChanging(object? sender, ScrollViewerViewChangingEventArgs e)
    {
        ScrollHeader(e.NextView.VerticalOffset);
    }


    private void VsGroupHeader_CurrentStateChanging(object sender, VisualStateChangedEventArgs e)
    {
        if (_scrollViewer != null)
            ScrollHeader(_scrollViewer.VerticalOffset);
    }



    public static ScrollViewer? GetScrollViewer(DependencyObject depObj)
    {
        if (depObj is ScrollViewer)
            return depObj as ScrollViewer;

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(depObj, i);

            ScrollViewer? result = GetScrollViewer(child);
            if (result != null)
                return result;
        }

        return null;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                if (_scrollViewer != null)
                    _scrollViewer.ViewChanging -= Scroll_ViewChanging;

                if (_stateGroup != null)
                    _stateGroup.CurrentStateChanging -= VsGroupHeader_CurrentStateChanging;
            }

            disposedValue = true;
        }
    }


    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
