using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace Rok.Shared.Collections;

public class RangeObservableCollection<T> : ObservableCollection<T>
{
    /// <summary>
    /// Replaces the whole content of the collection with <paramref name="items"/>.
    /// Raises exactly one <see cref="NotifyCollectionChangedAction.Reset"/> notification:
    /// subscribers never observe the intermediate empty state.
    /// </summary>
    public virtual void InitWithAddRange(IEnumerable<T> items)
    {
        ReplaceRange(items, clearFirst: true);
    }

    /// <summary>
    /// Appends <paramref name="items"/> to the collection.
    /// Raises exactly one <see cref="NotifyCollectionChangedAction.Reset"/> notification,
    /// whatever the number of appended items.
    /// </summary>
    public virtual void AddRange(IEnumerable<T> items)
    {
        ReplaceRange(items, clearFirst: false);
    }


    private void ReplaceRange(IEnumerable<T> items, bool clearFirst)
    {
        CheckReentrancy();

        if (clearFirst)
            Items.Clear();

        foreach (T item in items)
            Items.Add(item);

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }


    public void UpdateItem(T item)
    {
        int index = IndexOf(item);
        if (index >= 0 && index < Count)
            base.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, item, item, index));
    }
}