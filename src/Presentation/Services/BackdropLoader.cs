namespace Rok.Services;

public class BackdropLoader(BackdropPicture _backdropPicture, ILogger<BackdropLoader> _logger) : IBackdropLoader
{
    public void LoadBackdrop(string artistName, Action<BitmapImage?> setBackdrop)
    {
        if (string.IsNullOrEmpty(artistName))
        {
            setBackdrop(null);
            return;
        }


        try
        {
            string filePath;
            List<string> backdrops = _backdropPicture.GetBackdrops(artistName);
            if (backdrops.Count > 0)
            {
                int index = Random.Shared.Next(backdrops.Count);
                filePath = backdrops[index];
            }
            else
            {
                filePath = _backdropPicture.GetRandomGenericBackdrop();
            }

            if (App.MainWindow.DispatcherQueue is { } dq)
            {
                dq.TryEnqueue(() =>
                {
                    setBackdrop(new BitmapImage(new Uri(filePath, UriKind.Absolute)));
                });
            }
            else
            {
                setBackdrop(new BitmapImage(new Uri(filePath, UriKind.Absolute)));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load backdrop for artist: {ArtistName}", artistName);
            setBackdrop(null);
        }
    }
}
