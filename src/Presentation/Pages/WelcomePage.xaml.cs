using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Rok.ViewModels.Start;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Rok.Pages;


public sealed partial class WelcomePage : Page
{
    private static readonly TimeSpan FallbackStreamStartDelay = TimeSpan.FromMilliseconds(1500);

    private readonly DispatcherQueueTimer _fallbackStreamStartTimer;

    public StartViewModel ViewModel { get; set; }

    public WelcomePage()
    {
        InitializeComponent();

        ViewModel = App.ServiceProvider.GetRequiredService<StartViewModel>();

        _fallbackStreamStartTimer = DispatcherQueue.CreateTimer();
        _fallbackStreamStartTimer.Interval = FallbackStreamStartDelay;
        _fallbackStreamStartTimer.IsRepeating = false;
        _fallbackStreamStartTimer.Tick += (_, _) => ViewModel.StartAlbumStream();
    }


    private async void Button_Click(object sender, RoutedEventArgs e)
    {
        FolderPicker folderPicker = new()
        {
            ViewMode = PickerViewMode.List,
            SuggestedStartLocation = PickerLocationId.MusicLibrary
        };

        InitializeWithWindow.Initialize(folderPicker, Rok.App.MainWindowHandle);

        StorageFolder? folder = await folderPicker.PickSingleFolderAsync();

        if (folder is not null)
            ViewModel.AddLibraryFolderCommand.Execute(folder);
    }

    private void Grid_Loaded(object sender, RoutedEventArgs e)
    {
        FadeInStoryboard.Begin();
        _fallbackStreamStartTimer.Start();
        ViewModel.StartInitialScan();
    }

    private void FadeInStoryboard_Completed(object sender, object e)
    {
        _fallbackStreamStartTimer.Stop();
        ViewModel.StartAlbumStream();
    }
}