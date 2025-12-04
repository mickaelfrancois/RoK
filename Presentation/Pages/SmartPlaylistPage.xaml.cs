using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Rok.Commons;
using Rok.Logic.ViewModels.Playlist;
using Rok.Logic.ViewModels.Playlists;

namespace Rok.Pages;

public sealed partial class SmartPlaylistPage : Page
{
    public PlaylistViewModel ViewModel { get; set; }
    private readonly ResourceLoader _resourceLoader;

    public SmartPlaylistPage()
    {
        InitializeComponent();

        _resourceLoader = App.ServiceProvider.GetRequiredService<ResourceLoader>();
        ViewModel = App.ServiceProvider.GetRequiredService<PlaylistViewModel>();
        DataContext = ViewModel;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        if (e.Parameter is not PlaylistOpenArgs options)
            throw new ArgumentNullException(nameof(e), "PlaylistOpenArgs cannot be null");

        if (options.PlaylistId.HasValue)
            await ViewModel.LoadDataAsync(options.PlaylistId.Value);

        if (ViewModel.Playlist.Groups.Count > 0)
            LoadGroups();
        else
            GroupNew_Click(this, new RoutedEventArgs());

        if (ViewModel.Tracks.Count > 0)
            playlistPivot.SelectedItem = playlistViewTabTracks;

        base.OnNavigatedTo(e);
    }

    private void LoadGroups()
    {
        foreach (PlaylistGroupDto groupDto in ViewModel.Playlist.Groups)
        {
            PlaylistGroup group = CreateGroup();
            group.Group = groupDto;

            lvGroup.Items.Add(group);
        }
    }


    private void GroupNew_Click(object sender, RoutedEventArgs arg)
    {
        PlaylistGroupDto groupDto = new()
        {
            Name = _resourceLoader.GetString("groupDefaultName"),
            TrackCount = 20
        };

        groupDto.Filters.Add(new PlaylistFilterDto { Entity = SmartPlaylistEntity.Tracks, Field = SmartPlaylistField.Score, FieldType = SmartPlaylistFieldType.Int, Operator = SmartPlaylistOperator.GreaterThan, Value = "0" });

        PlaylistGroup group = CreateGroup();
        group.Group = groupDto;
        lvGroup.Items.Add(group);
    }


    private PlaylistGroup CreateGroup()
    {
        PlaylistGroup group = new(_resourceLoader);
        group.RemoveGroupClicked += Group_RemoveGroupClicked;
        return group;
    }


    private void Group_RemoveGroupClicked(object? sender, EventArgs e)
    {
        if (sender == null)
            return;

        if (lvGroup.Items.Count == 1)
            return;

        PlaylistGroup group = (PlaylistGroup)sender;
        group.ClearFilters();

        group.RemoveGroupClicked -= Group_RemoveGroupClicked;
        lvGroup.Items.Remove(group);
    }


    private void PlaylistSave_Click(object? sender, RoutedEventArgs e)
    {
        List<PlaylistGroupDto> groups = [];

        foreach (PlaylistGroup group in lvGroup.Items.Cast<PlaylistGroup>())
            groups.Add(group.Group);

        if (ViewModel.SavePlaylistCommand.CanExecute(groups))
            ViewModel.SavePlaylistCommand.Execute(groups);
    }


    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            Title = _resourceLoader.GetString("DeleteConfirmationTitle"),
            Content = _resourceLoader.GetString("DeletePlaylistConfirmation"),
            PrimaryButtonText = _resourceLoader.GetString("YesButton"),
            CloseButtonText = _resourceLoader.GetString("CancelButton"),
            DefaultButton = ContentDialogButton.Close
        };

        ContentDialogResult result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary && ViewModel.DeleteCommand.CanExecute(null))
            ViewModel.DeleteCommand.Execute(null);
    }
}
