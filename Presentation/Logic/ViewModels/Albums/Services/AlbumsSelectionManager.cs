namespace Rok.Logic.ViewModels.Albums.Services;

public partial class AlbumsSelectionManager : ObservableObject
{
    public ObservableCollection<object> Selected { get; } = [];

    public List<AlbumViewModel> SelectedItems
    {
        get
        {
            List<AlbumViewModel> list = [];

            if (Selected.Count > 0)
                list.AddRange(Selected.Select(c => (AlbumViewModel)c));

            return list;
        }
    }

    public int SelectedCount => Selected.Count;

    public bool IsSelectedItems => SelectedCount > 0;

    public event EventHandler? SelectionChanged;

    public AlbumsSelectionManager()
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