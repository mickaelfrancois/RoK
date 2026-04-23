using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Rok.Services;

public static class ImagePickerService
{
    public static async Task<StorageFile?> PickAsync()
    {
        IntPtr windowHandle = App.MainWindowHandle;

        FileOpenPicker openPicker = new()
        {
            ViewMode = PickerViewMode.Thumbnail,
            SuggestedStartLocation = PickerLocationId.Downloads
        };

        openPicker.FileTypeFilter.Add(".jpg");
        openPicker.FileTypeFilter.Add(".jpeg");
        openPicker.FileTypeFilter.Add(".png");

        if (IsWindows11OrGreater())
            openPicker.FileTypeFilter.Add(".webp");

        InitializeWithWindow.Initialize(openPicker, windowHandle);

        return await openPicker.PickSingleFileAsync();
    }

    private static bool IsWindows11OrGreater()
    {
        Version osVersion = Environment.OSVersion.Version;
        // Windows 11: Major=10, Build >= 22000
        return osVersion.Major == 10 && osVersion.Build >= 22000;
    }
}