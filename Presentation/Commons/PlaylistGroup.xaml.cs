using Microsoft.UI.Xaml.Controls;
using System.Globalization;


namespace Rok.Commons;

public sealed partial class PlaylistGroup : UserControl
{
    private const int KGroupTrackCount = 10;

    public event EventHandler<EventArgs>? RemoveGroupClicked;

    private readonly CompareInfo _compareInfo = CultureInfo.CurrentCulture.CompareInfo;

    private const string DisplayMemberValuePath = "Label";

    public PlaylistGroupDto Group
    {
        get
        {
            return new PlaylistGroupDto()
            {
                Name = GroupName,
                TrackCount = TrackCount,
                Filters = Filters,
                SortBy = SortBy.Key
            };
        }
        set
        {
            GroupName = value.Name;
            TrackCount = value.TrackCount;

            cbGroupSortBy.SelectedItem = ((IEnumerable<SortByOption>)cbGroupSortBy.ItemsSource).FirstOrDefault(f => f.Key == value.SortBy);

            ClearFilters();

            foreach (PlaylistFilterDto filter in value.Filters)
            {
                NewFilter(filter);
            }
        }
    }

    public SortByOption SortBy
    {
        get
        {
            return (SortByOption)cbGroupSortBy.SelectedItem;
        }
        set
        {
            cbGroupSortBy.SelectedItem = value;
        }
    }

    public string GroupName { get { return groupName.Text; } set { groupName.Text = value; } }
    public int TrackCount { get { return (int)trackCount.Value; } set { trackCount.Value = value; } }

    public List<PlaylistFilterDto> Filters
    {
        get
        {
            List<PlaylistFilterDto> filters = [];

            foreach (PlaylistGroupFilter filter in listFilters.Items.Cast<PlaylistGroupFilter>())
            {
                filters.Add(filter.Filter);
            }

            return filters;
        }
    }

    private readonly ResourceLoader _resourceLoader;

    public PlaylistGroup(ResourceLoader resourceLoader)
    {
        InitializeComponent();

        _resourceLoader = resourceLoader;

        GroupName = "Groupe";
        TrackCount = KGroupTrackCount;

        InitSortList();
    }


    private void InitSortList()
    {
        List<SortByOption> sorts = new()
        {
             new SortByOption(SmartPlaylistSelectBy.Random, _resourceLoader.GetString("playlistGroupSortRandom")),
                new SortByOption(SmartPlaylistSelectBy.MostPlayed, _resourceLoader.GetString("playlistGroupSortMostPlayed")),
                new SortByOption(SmartPlaylistSelectBy.LeastPlayed, _resourceLoader.GetString("playlistGroupSortLeastPlayed")),
                new SortByOption(SmartPlaylistSelectBy.MostRecent, _resourceLoader.GetString("playlistGroupSortMostRecent")),
                new SortByOption(SmartPlaylistSelectBy.LeastRecent, _resourceLoader.GetString("playlistGroupSortLeastRecent")),
                new SortByOption(SmartPlaylistSelectBy.HighestRated, _resourceLoader.GetString("playlistGroupSortHighestRated")),
                new SortByOption(SmartPlaylistSelectBy.LowestRated, _resourceLoader.GetString("playlistGroupSortLowestRated")),
                new SortByOption(SmartPlaylistSelectBy.Oldest, _resourceLoader.GetString("playlistGroupSortOldest")),
                new SortByOption(SmartPlaylistSelectBy.Newest, _resourceLoader.GetString("playlistGroupSortNewest")),
        };
        sorts.Sort((a, b) => _compareInfo.Compare(a.Label, b.Label, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace));

        cbGroupSortBy.ItemsSource = sorts;
        cbGroupSortBy.DisplayMemberPath = DisplayMemberValuePath;

        cbGroupSortBy.SelectedIndex = 0;
    }


    public void ClearFilters()
    {
        foreach (PlaylistGroupFilter filter in listFilters.Items.Cast<PlaylistGroupFilter>())
        {
            filter.NewFilterClicked -= FilterNew_Click;
            filter.RemoveFilterClicked -= FilterRemove_Click;
            listFilters.Items.Remove(filter);
        }
    }


    private PlaylistGroupFilter NewFilter()
    {
        PlaylistGroupFilter filter = new(_resourceLoader);
        filter.NewFilterClicked += FilterNew_Click;
        filter.RemoveFilterClicked += FilterRemove_Click;
        listFilters.Items.Add(filter);

        return filter;
    }

    private void NewFilter(PlaylistFilterDto filter)
    {
        PlaylistGroupFilter groupFilter = NewFilter();
        groupFilter.Filter = filter;
    }

    private void FilterRemove_Click(object? sender, EventArgs e)
    {
        PlaylistGroupFilter? filter = sender as PlaylistGroupFilter;
        if (filter == null)
            return;

        ListBox? parent = filter.Parent as ListBox;
        if (parent == null)
            return;

        if (parent.Items.Count <= 1)
            return;

        filter.NewFilterClicked -= FilterNew_Click;
        filter.RemoveFilterClicked -= FilterRemove_Click;

        parent.Items.Remove(filter);
    }

    private void FilterNew_Click(object? sender, EventArgs e)
    {
        NewFilter();
    }


    private void GroupRemove_Click(object? sender, RoutedEventArgs e)
    {
        RemoveGroupClicked?.Invoke(this, EventArgs.Empty);
    }

    public sealed class SortByOption(SmartPlaylistSelectBy key, string label)
    {
        public SmartPlaylistSelectBy Key { get; init; } = key;
        public string Label { get; init; } = label;
    }
}
