namespace Rok.Logic.ViewModels.Tracks.Services;

public partial class TracksSelectionManager : ObservableObject
{
    public ObservableCollection<object> Selected { get; } = [];

    public List<TrackViewModel> SelectedItems
    {
        get
        {
            List<TrackViewModel> list = [];

            if (Selected.Count > 0)
                list.AddRange(Selected.Select(c => (TrackViewModel)c));

            return list;
        }
    }

    public int SelectedCount => Selected.Count;

    public bool IsSelectedItems => SelectedCount > 0;

    public event EventHandler? SelectionChanged;

    public TracksSelectionManager()
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
