using Microsoft.UI.Xaml.Controls;
using Rok.ViewModels.Start;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Rok.Pages;


public sealed partial class WelcomePage : Page
{
    public StartViewModel ViewModel { get; set; }

    public WelcomePage()
    {
        InitializeComponent();

        ViewModel = App.ServiceProvider.GetRequiredService<StartViewModel>();
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
        ViewModel.StartInitialScan();
    }
}