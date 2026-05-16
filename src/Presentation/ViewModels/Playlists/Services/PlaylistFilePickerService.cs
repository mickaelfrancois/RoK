using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Rok.ViewModels.Playlists.Services;

public sealed class PlaylistFilePickerService : IPlaylistFilePickerService
{
    public async Task<IReadOnlyList<string>> PickPlaylistFilesAsync()
    {
        FileOpenPicker picker = new()
        {
            ViewMode = PickerViewMode.List,
            SuggestedStartLocation = PickerLocationId.MusicLibrary
        };
        picker.FileTypeFilter.Add(".m3u");
        picker.FileTypeFilter.Add(".m3u8");

        InitializeWithWindow.Initialize(picker, App.MainWindowHandle);

        IReadOnlyList<StorageFile> files = await picker.PickMultipleFilesAsync();
        return files.Select(f => f.Path).ToList();
    }

    public async Task<string?> PickSavePathAsync(string suggestedFileName)
    {
        FileSavePicker picker = new()
        {
            SuggestedStartLocation = PickerLocationId.MusicLibrary,
            SuggestedFileName = suggestedFileName,
            DefaultFileExtension = ".m3u8"
        };
        picker.FileTypeChoices.Add("M3U8 playlist", new List<string> { ".m3u8" });

        InitializeWithWindow.Initialize(picker, App.MainWindowHandle);

        StorageFile? file = await picker.PickSaveFileAsync();
        return file?.Path;
    }
}