using Microsoft.UI.Xaml.Controls;
using Rok.Logic.ViewModels.Player;

namespace Rok.Commons;

public sealed partial class CompactPlayerControl : UserControl
{
    public PlayerViewModel PlayerViewModel { get; set; }

    public CompactPlayerControl()
    {
        InitializeComponent();

        PlayerViewModel = App.ServiceProvider.GetRequiredService<PlayerViewModel>();
        DataContext = PlayerViewModel;
    }


    private void Grid_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        if (PlayerViewModel.CompactModeCommand.CanExecute(null))
            PlayerViewModel.CompactModeCommand.Execute(null);
    }
}
