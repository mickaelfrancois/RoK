using Microsoft.UI.Xaml.Controls;

namespace Rok.Commons;

public sealed partial class GridViewExtended : GridView
{
    public ObservableCollection<object> BindableSelectedItems
    {
        get => GetValue(BindableSelectedItemsProperty) as ObservableCollection<object> ?? new ObservableCollection<object>();
        set => SetValue(BindableSelectedItemsProperty, value);
    }

    public static readonly DependencyProperty BindableSelectedItemsProperty = DependencyProperty.Register("BindableSelectedItems", typeof(ObservableCollection<object>), typeof(GridViewExtended),
        new PropertyMetadata(null, (s, e) =>
        {
            GridViewExtended? gridView = s as GridViewExtended;
            if (gridView != null)
            {
                gridView.SelectionChanged -= gridView.MyGridView_SelectionChanged;
                gridView.SelectionChanged += gridView.MyGridView_SelectionChanged;
            }
        }));


    private void MyGridView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (BindableSelectedItems == null)
            return;

        foreach (object item in BindableSelectedItems.Where(x => !SelectedItems.Contains(x)).ToArray())
            BindableSelectedItems.Remove(item);

        foreach (object item in SelectedItems.Where(x => !BindableSelectedItems.Contains(x)))
            BindableSelectedItems.Add(item);
    }
}