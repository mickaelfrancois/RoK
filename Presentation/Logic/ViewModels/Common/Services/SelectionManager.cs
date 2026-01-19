namespace Rok.Logic.ViewModels.Common.Services;

public abstract partial class SelectionManager<T> : MyObservableObject
{
    public ObservableCollection<object> Selected { get; } = [];

    public List<T> SelectedItems
    {
        get
        {
            List<T> list = [];

            if (Selected.Count > 0)
                list.AddRange(Selected.Select(c => (T)c));

            return list;
        }
    }

    public int SelectedCount => Selected.Count;

    public bool IsSelectedItems => SelectedCount > 0;

    public event EventHandler? SelectionChanged;

    protected SelectionManager()
    {
        Selected.CollectionChanged += (s, e) =>
        {
            OnPropertyChanged(nameof(SelectedItems));
            OnPropertyChanged(nameof(SelectedCount));
            OnPropertyChanged(nameof(IsSelectedItems));
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        };
    }
}