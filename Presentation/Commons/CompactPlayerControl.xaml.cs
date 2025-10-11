using Microsoft.UI.Xaml.Controls;
using Rok.Logic.ViewModels.Player;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

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
}
