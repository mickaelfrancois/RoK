using Microsoft.UI.Xaml.Controls;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using WinRT.Interop;


namespace Rok.Pages;

public sealed partial class OptionsPage : Page
{
    public IAppOptions Options { get; }

    public string ThemeString
    {
        get => Options.Theme.ToString();
        set
        {
            if (Enum.TryParse<AppTheme>(value, out AppTheme theme))
                Options.Theme = theme;
        }
    }

    public OptionsPage()
    {
        InitializeComponent();

        Options = App.ServiceProvider.GetRequiredService<IAppOptions>();
    }

    private async void AddLibraryFolderButton_Click(object? sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        FolderPicker folderPicker = new()
        {
            ViewMode = PickerViewMode.List,
            SuggestedStartLocation = PickerLocationId.MusicLibrary
        };

        InitializeWithWindow.Initialize(folderPicker, Rok.App.MainWindowHandle);

        StorageFolder? folder = null;

        try
        {
            folder = await folderPicker.PickSingleFolderAsync();
        }
        catch
        {
            // Ignore            
        }

        if (folder is null)
            return;

        Options.LibraryTokens ??= new List<string>();
        Options.LibraryPath ??= new List<string>();

        if (Options.LibraryPath.Any(p => string.Equals(p, folder.Path, StringComparison.OrdinalIgnoreCase)))
            return;

        string token = StorageApplicationPermissions.FutureAccessList.Add(folder);

        Options.LibraryTokens.Add(token);
        Options.LibraryPath.Add(folder.Path);

        LibraryPathsList.ItemsSource = null;
        LibraryPathsList.ItemsSource = Options.LibraryPath;
    }

    private void RemoveLibraryFolderButton_Click(object? sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        try
        {
            if (LibraryPathsList.SelectedItem is not string selectedPath)
                return;

            if (Options.LibraryPath?.Count <= 1)
                return;

            Options.LibraryPath?.RemoveAll(p => string.Equals(p, selectedPath, StringComparison.OrdinalIgnoreCase));
            Options.LibraryTokens?.RemoveAll(t => string.Equals(t, selectedPath, StringComparison.OrdinalIgnoreCase));

            try
            {
                if (StorageApplicationPermissions.FutureAccessList.ContainsItem(selectedPath))
                    StorageApplicationPermissions.FutureAccessList.Remove(selectedPath);
            }
            catch
            {
                // Ignore
            }

            LibraryPathsList.ItemsSource = null;
            LibraryPathsList.ItemsSource = Options.LibraryPath;
        }
        catch
        {
            // Ignore
        }
    }
}
