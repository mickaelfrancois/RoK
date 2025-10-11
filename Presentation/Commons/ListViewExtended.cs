using Microsoft.UI.Xaml.Controls;
using System.Collections.Specialized;

namespace Rok.Commons;


public sealed partial class ListViewExtended : ListView, IDisposable
{
    private bool _suppressSync;
    private bool disposedValue;

    public ObservableCollection<object>? BindableSelectedItems
    {
        get => GetValue(BindableSelectedItemsProperty) as ObservableCollection<object>;
        set => SetValue(BindableSelectedItemsProperty, value);
    }

    public static readonly DependencyProperty BindableSelectedItemsProperty =
        DependencyProperty.Register(
            nameof(BindableSelectedItems),
            typeof(ObservableCollection<object>),
            typeof(ListViewExtended),
            new PropertyMetadata(null, OnBindableSelectedItemsChanged));

    private static void OnBindableSelectedItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ListViewExtended listView)
            return;

        if (e.OldValue is ObservableCollection<object> oldCol)
            oldCol.CollectionChanged -= listView.BindableSelectedItems_CollectionChanged;

        if (e.NewValue is ObservableCollection<object> newCol)
        {
            newCol.CollectionChanged += listView.BindableSelectedItems_CollectionChanged;
            listView.SyncFromCollectionToListView();
        }
    }


    public ListViewExtended()
    {
        SelectionChanged += OnSelectionChanged;
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSync)
            return;

        SyncFromListViewToCollection();
    }

    private void BindableSelectedItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_suppressSync)
            return;

        SyncFromCollectionToListView();
    }

    private void SyncFromListViewToCollection()
    {
        if (BindableSelectedItems is null)
            return;

        try
        {
            _suppressSync = true;

            List<object> itemsToRemove = BindableSelectedItems
                .Where(item => !SelectedItems.Contains(item))
                .ToList();

            foreach (object? item in itemsToRemove)
            {
                BindableSelectedItems.Remove(item);
            }


            List<object> itemsToAdd = SelectedItems.Cast<object>()
                .Where(item => !BindableSelectedItems.Contains(item))
                .ToList();
            foreach (object? item in itemsToAdd)
            {
                BindableSelectedItems.Add(item);
            }
        }
        finally
        {
            _suppressSync = false;
        }
    }

    private void SyncFromCollectionToListView()
    {
        if (BindableSelectedItems is null)
            return;

        try
        {
            _suppressSync = true;

            List<object> itemsToRemove = SelectedItems.Cast<object>()
                .Where(item => !BindableSelectedItems.Contains(item))
                .ToList();

            foreach (object item in itemsToRemove)
            {
                SelectedItems.Remove(item);
            }

            List<object> itemsToAdd = BindableSelectedItems
                .Where(item => !SelectedItems.Contains(item))
                .ToList();

            foreach (object? item in itemsToAdd)
            {
                SelectedItems.Add(item);
            }
        }
        finally
        {
            _suppressSync = false;
        }
    }

    private void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                SelectionChanged -= OnSelectionChanged;
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