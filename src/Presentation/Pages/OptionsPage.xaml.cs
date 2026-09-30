using Microsoft.UI.Xaml.Controls;
using Rok.Application.Player;
using Rok.Application.Player.Output;
using Rok.ViewModels.Statistics;
using Windows.ApplicationModel;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using Windows.System;
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

    public string ReplayGainModeString
    {
        get => Options.ReplayGainMode.ToString();
        set
        {
            if (!Enum.TryParse(value, out EReplayGainMode mode) || mode == Options.ReplayGainMode)
                return;

            Options.ReplayGainMode = mode;
            _messenger.Send(new ReplayGainOptionsChanged());
        }
    }

    public double ReplayGainPreampDb
    {
        get => Options.ReplayGainPreampDb;
        set
        {
            double preamp = Math.Clamp(value, ReplayGainCalculator.MinPreampDb, ReplayGainCalculator.MaxPreampDb);

            if (preamp.Equals(Options.ReplayGainPreampDb))
                return;

            Options.ReplayGainPreampDb = preamp;
            _messenger.Send(new ReplayGainOptionsChanged());
        }
    }

    public string OutputModeString
    {
        get => Options.OutputMode.ToString();
        set
        {
            if (!Enum.TryParse(value, out EAudioOutputMode mode) || mode == Options.OutputMode)
                return;

            Options.OutputMode = mode;
            _messenger.Send(new AudioOutputOptionsChanged());
        }
    }

    public double ReplayGainPreampMin => ReplayGainCalculator.MinPreampDb;

    public double ReplayGainPreampMax => ReplayGainCalculator.MaxPreampDb;

    public double ReplayGainPreampStep => ReplayGainCalculator.PreampStepDb;

    private readonly IMessenger _messenger;
    private readonly IFolderResolver _folderResolver;
    private readonly ResourceLoader _resourceLoader;
    private readonly ILogger<OptionsPage> _logger;
    private readonly IAudioDeviceService _audioDeviceService;
    private readonly IPlayerEngine _playerEngine;
    private IDisposable? _outputStateSubscription;
    private bool _isLoadingDevices;

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
        _messenger = App.ServiceProvider.GetRequiredService<IMessenger>();
        _folderResolver = App.ServiceProvider.GetRequiredService<IFolderResolver>();
        _resourceLoader = App.ServiceProvider.GetRequiredService<ResourceLoader>();
        _logger = App.ServiceProvider.GetRequiredService<ILogger<OptionsPage>>();
        _audioDeviceService = App.ServiceProvider.GetRequiredService<IAudioDeviceService>();
        _playerEngine = App.ServiceProvider.GetRequiredService<IPlayerEngine>();

        StatisticsViewModel = App.ServiceProvider.GetRequiredService<StatisticsViewModel>();
    }

    protected override async void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        LoadOutputDevices();
        _outputStateSubscription = _messenger.Subscribe<AudioOutputStateChanged>(message =>
            DispatcherQueue.TryEnqueue(() => ShowOutputStatus(message.State, message.IsLive)));
        ShowOutputStatus(_playerEngine.OutputState, _playerEngine.IsLive);

        try
        {
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
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Navigation to OptionsPage failed");
        }
    }

    protected override void OnNavigatedFrom(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        _outputStateSubscription?.Dispose();
        _outputStateSubscription = null;

        base.OnNavigatedFrom(e);
    }

    /// <summary>
    /// The Windows default device first, then the active devices. A chosen device that is unplugged stays listed as
    /// disconnected, so opening the options never replaces the saved choice.
    /// </summary>
    private void LoadOutputDevices()
    {
        List<OutputDeviceItem> items = [new(string.Empty, _resourceLoader.GetString("OptionsOutputDeviceDefault"))];

        try
        {
            items.AddRange(_audioDeviceService.GetSnapshot().Active.Select(device => new OutputDeviceItem(device.Id, device.Name)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to list the audio output devices");
        }

        string savedId = Options.OutputDeviceId ?? string.Empty;

        if (!items.Exists(item => item.Id == savedId))
            items.Add(new OutputDeviceItem(savedId, _resourceLoader.GetString("OptionsOutputDeviceDisconnected")));

        List<ComboBoxItem> entries = [.. items.Select(item => new ComboBoxItem { Content = item.Name, Tag = item.Id })];

        _isLoadingDevices = true;
        OutputDeviceComboBox.ItemsSource = entries;
        OutputDeviceComboBox.SelectedItem = entries.Find(entry => (string)entry.Tag == savedId);
        _isLoadingDevices = false;
    }

    private void OutputDeviceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingDevices || OutputDeviceComboBox.SelectedItem is not ComboBoxItem { Tag: string deviceId } || deviceId == Options.OutputDeviceId)
            return;

        Options.OutputDeviceId = deviceId;
        _messenger.Send(new AudioOutputOptionsChanged());
    }

    private void ShowOutputStatus(AudioOutputState state, bool isLive) =>
        OutputStatusText.Text = _resourceLoader.GetString(AudioOutputTextKeys.Status(AudioOutputStatusResolver.Resolve(state, isLive)));

    private sealed record OutputDeviceItem(string Id, string Name);

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
            await StatisticsViewModel.ResetListenCountCommand.ExecuteAsync(null);
    }

    private void SupportButton_Click(object sender, RoutedEventArgs e)
    {
        Uri uri = new("https://www.buymeacoffee.com/mickaelfrancois");
        _ = Windows.System.Launcher.LaunchUriAsync(uri);
    }


    private async void TestEndpoint_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
            return;

        string endpoint = button.Tag?.ToString() ?? string.Empty;
        string url = $"http://localhost:{Options.WebApiPort}{endpoint}";

        Uri uri = new(url);
        await Windows.System.Launcher.LaunchUriAsync(uri);
    }

    private void OfficialPageButton_Click(object sender, RoutedEventArgs e)
    {
        Uri uri = new("https://rok.fpc-france.com");
        _ = Windows.System.Launcher.LaunchUriAsync(uri);
    }

    private async void OnLeaveReviewClicked(object sender, RoutedEventArgs e)
    {
        await Launcher.LaunchUriAsync(new Uri($"ms-windows-store://review/?ProductId=9NX19R28Q92S"));
    }

    public string SelectedLanguage
    {
        get => Options.Language ?? "System";
        set
        {
            if (value == "System")
            {
                Options.Language = null;
                Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = string.Empty;
            }
            else
            {
                Options.Language = value;
                Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = value;
            }
        }
    }

    public List<string> AvailableLanguages { get; } = new()
    {
        "System", // System language
        "en-US", // English
        "fr-FR", // French
        "es-ES", // Spanish (Automatic Translation)
        "uk-UA" // Ukrainian (Automatic Translation)
    };
}

public class PathItem(string key, string value)
{
    public string Key { get; } = key;

    public string Value { get; } = value;
}