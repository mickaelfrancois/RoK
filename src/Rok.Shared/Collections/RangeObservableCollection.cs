using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace Rok.Shared.Collections;

public class RangeObservableCollection<T> : ObservableCollection<T>
{
    private bool _suppressEvents;

    /// <summary>
    /// Clear collection and add a list of items.
    /// </summary>
    public virtual void InitWithAddRange(IEnumerable<T> items)
    {
        Clear();
        AddRange(items);
    }

    public virtual void AddRange(IEnumerable<T> items)
    {
        _suppressEvents = true;

        foreach (T item in items)
            Add(item);

        _suppressEvents = false;

        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }


    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        if (!_suppressEvents)
            base.OnCollectionChanged(e);
    }


    public void UpdateItem(T item)
    {
        int index = IndexOf(item);
        if (index >= 0 && index < Count)
            base.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, item, item, index));
    }
}