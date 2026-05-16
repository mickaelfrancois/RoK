namespace Rok.ViewModels.Playlist.Services;

public interface IPlaylistExportPrompts
{
    Task<bool> ConfirmSmartPlaylistExportAsync();

    Task<string?> PickSavePathAsync(string suggestedFileName);
}