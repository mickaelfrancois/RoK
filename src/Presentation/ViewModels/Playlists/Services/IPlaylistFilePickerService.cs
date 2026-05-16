namespace Rok.ViewModels.Playlists.Services;

public interface IPlaylistFilePickerService
{
    Task<IReadOnlyList<string>> PickPlaylistFilesAsync();

    Task<string?> PickSavePathAsync(string suggestedFileName);
}