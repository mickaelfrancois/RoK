using Microsoft.UI.Xaml.Controls;
using Rok.Logic.ViewModels.Player;

namespace Rok.Commons;

public sealed partial class FullScreenControl : UserControl
{
    public PlayerViewModel PlayerViewModel { get; set; }

    //public TrackViewModel CurrentTrack { get { return PlayerViewModel.CurrentTrack; } }

    private int _animationIndex = 1;
    private readonly int _maxAnimation = 3;



    public FullScreenControl()
    {
        InitializeComponent();

        PlayerViewModel = App.ServiceProvider.GetRequiredService<PlayerViewModel>();
        DataContext = PlayerViewModel;
    }


    private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ListBox? list = sender as ListBox;

        if (list != null && e.AddedItems.Count > 0)
            list.ScrollIntoView(e.AddedItems[0]);
    }


    public void TrackChanged()
    {
        _animationIndex++;

        if (_animationIndex > _maxAnimation)
            _animationIndex = 1;

        switch (_animationIndex)
        {
            case 1:
                changeTrackAnimation1?.Storyboard.Begin();
                break;

            case 2:
                changeTrackAnimation2?.Storyboard.Begin();
                break;

            case 3:
                changeTrackAnimation3?.Storyboard.Begin();
                break;
        }
    }
}
