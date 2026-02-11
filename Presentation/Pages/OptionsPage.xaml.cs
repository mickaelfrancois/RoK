using Microsoft.UI.Xaml.Controls;
using Rok.Logic.ViewModels.Statistics;
using Windows.ApplicationModel;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using WinRT.Interop;


namespace Rok.Pages;

public sealed partial class OptionsPage : Page
{
    public IAppOptions Options { get; }

    private readonly List<PathItem> _paths = [];
    public List<PathItem> Paths { get => _paths; }

    public string ThemeString
    {
        get => Options.Theme.ToString();
        set
        {
            if (Enum.TryParse<AppTheme>(value, out AppTheme theme))
                Options.Theme = theme;
        }
    }

    private readonly IFolderResolver _folderResolver;

    private readonly ResourceLoader _resourceLoader;

    private StatisticsViewModel StatisticsViewModel { get; }

    public string AppVersionString
    {
        get
        {
            PackageVersion version = Windows.ApplicationModel.Package.Current.Id.Version;
            return $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
        }
    }


    public OptionsPage()
    {
        InitializeComponent();

        Options = App.ServiceProvider.GetRequiredService<IAppOptions>();
        _folderResolver = App.ServiceProvider.GetRequiredService<IFolderResolver>();
        _resourceLoader = App.ServiceProvider.GetRequiredService<ResourceLoader>();

        StatisticsViewModel = App.ServiceProvider.GetRequiredService<StatisticsViewModel>();
    }

    protected override async void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        _paths.Clear();

        foreach (string token in Options.LibraryTokens ?? Enumerable.Empty<string>())
        {
            string? path = await _folderResolver.GetDisplayNameFromTokenAsync(token);
            if (path is not null)
            {
                _paths.Add(new PathItem(token, path));
            }
        }

        await StatisticsViewModel.LoadAsync();
    }

    private void StorePageButton_Click(object sender, RoutedEventArgs e)
    {
        Uri uri = new("https://apps.microsoft.com/store/detail/9NX19R28Q92S?cid=DevShareMCLPCS");
        _ = Windows.System.Launcher.LaunchUriAsync(uri);
    }

    private void GitHubPageButton_Click(object sender, RoutedEventArgs e)
    {
        Uri uri = new("https://github.com/mickaelfrancois/RoK");
        _ = Windows.System.Launcher.LaunchUriAsync(uri);
    }

    private async void OpenLogButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            await Windows.System.Launcher.LaunchFolderAsync(localFolder);
        }
        catch
        {
            // Ignore
        }
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

        string token = StorageApplicationPermissions.FutureAccessList.Add(folder);

        Options.LibraryTokens ??= new List<string>();

        if (Options.LibraryTokens.Any(p => string.Equals(p, token, StringComparison.OrdinalIgnoreCase)))
            return;

        Options.LibraryTokens.Add(token);

        Paths.Add(new PathItem(token, folder.Path));

        LibraryPathsList.ItemsSource = null;
        LibraryPathsList.ItemsSource = Paths;
    }

    private void RemoveLibraryFolderButton_Click(object? sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        try
        {
            if (LibraryPathsList.SelectedItem is not PathItem selectedPath)
                return;

            if (Options.LibraryTokens?.Count <= 1)
                return;

            Options.LibraryTokens?.RemoveAll(t => string.Equals(t, selectedPath.Key, StringComparison.OrdinalIgnoreCase));
            Paths.RemoveAll(p => string.Equals(p.Key, selectedPath.Key, StringComparison.OrdinalIgnoreCase));

            try
            {
                if (StorageApplicationPermissions.FutureAccessList.ContainsItem(selectedPath.Key))
                    StorageApplicationPermissions.FutureAccessList.Remove(selectedPath.Key);
            }
            catch
            {
                // Ignore
            }

            LibraryPathsList.ItemsSource = null;
            LibraryPathsList.ItemsSource = Paths;
        }
        catch
        {
            // Ignore
        }
    }

    private async void ResetListenCount_Click(object sender, RoutedEventArgs e)
    {
        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            Title = _resourceLoader.GetString("ResetListenCountConfirmationTitle"),
            Content = _resourceLoader.GetString("ResetListenCountTitleConfirmation"),
            PrimaryButtonText = _resourceLoader.GetString("YesButton"),
            CloseButtonText = _resourceLoader.GetString("CancelButton"),
            DefaultButton = ContentDialogButton.Close
        };

        ContentDialogResult result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary && StatisticsViewModel.ResetListenCountCommand.CanExecute(null))
            StatisticsViewModel.ResetListenCountCommand.Execute(null);
    }

    private void SupportButton_Click(object sender, RoutedEventArgs e)
    {
        Uri uri = new("https://www.buymeacoffee.com/mickaelfrancois");
        _ = Windows.System.Launcher.LaunchUriAsync(uri);
    }
}

public class PathItem(string key, string value)
{
    public string Key { get; } = key;

    public string Value { get; } = value;
}
