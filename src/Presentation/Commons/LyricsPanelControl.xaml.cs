using Microsoft.UI.Xaml.Controls;
using Rok.ViewModels.Player;

namespace Rok.Commons;

/// <summary>
/// Side panel that shows the lyrics of the current track next to the page content.
/// It only reads the state already computed by <see cref="PlayerViewModel"/>.
/// </summary>
public sealed partial class LyricsPanelControl : UserControl
{
    public PlayerViewModel PlayerViewModel { get; }

    public LyricsPanelControl()
    {
        PlayerViewModel = App.ServiceProvider.GetRequiredService<PlayerViewModel>();

        InitializeComponent();
    }
}