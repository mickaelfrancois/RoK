namespace Rok.Logic.ViewModels.Artists.Services;

public partial class ArtistsSelectionManager : ObservableObject
{
    public ObservableCollection<object> Selected { get; } = [];

    public List<ArtistViewModel> SelectedItems
    {
        get
        {
            List<ArtistViewModel> list = [];

            if (Selected.Count > 0)
                list.AddRange(Selected.Select(c => (ArtistViewModel)c));

            return list;
        }
    }

    public int SelectedCount => Selected.Count;

    public bool IsSelectedItems => SelectedCount > 0;

    public event EventHandler? SelectionChanged;

    public ArtistsSelectionManager()
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