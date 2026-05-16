using Microsoft.UI.Xaml.Controls;
using Rok.ViewModels.Playlists.Services;

namespace Rok.ViewModels.Playlist.Services;

public sealed class PlaylistExportPrompts(IPlaylistFilePickerService _picker, ResourceLoader _resourceLoader) : IPlaylistExportPrompts
{
    public async Task<bool> ConfirmSmartPlaylistExportAsync()
    {
        ContentDialog dialog = new()
        {
            XamlRoot = App.MainWindow.Content.XamlRoot,
            Title = _resourceLoader.GetString("ExportSmartPlaylistTitle"),
            Content = _resourceLoader.GetString("ExportSmartPlaylistMessage"),
            PrimaryButtonText = _resourceLoader.GetString("YesButton"),
            CloseButtonText = _resourceLoader.GetString("CancelButton"),
            DefaultButton = ContentDialogButton.Primary
        };

        ContentDialogResult result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    public Task<string?> PickSavePathAsync(string suggestedFileName)
        => _picker.PickSavePathAsync(suggestedFileName);
}