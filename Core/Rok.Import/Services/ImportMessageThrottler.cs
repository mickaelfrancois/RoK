namespace Rok.Import.Services;

public class ImportMessageThrottler
{
    private const int MaxMessagesBeforeThrottle = 40;
    private int _albumMessagesSent = 0;
    private bool _isAlbumThrottled = false;
    private int _artistMessagesSent = 0;
    private bool _isArtistThrottled = false;

    public bool ShouldSendAlbumMessage()
    {
        if (_isAlbumThrottled)
            return false;

        _albumMessagesSent++;

        if (_albumMessagesSent >= MaxMessagesBeforeThrottle)
        {
            _isAlbumThrottled = true;
            return false;
        }

        return true;
    }

    public bool ShouldSendArtistMessage()
    {
        if (_isArtistThrottled)
            return false;

        _artistMessagesSent++;

        if (_artistMessagesSent >= MaxMessagesBeforeThrottle)
        {
            _isArtistThrottled = true;
            return false;
        }

        return true;
    }

    public void Reset()
    {
        _albumMessagesSent = 0;
        _isAlbumThrottled = false;
        _artistMessagesSent = 0;
        _isArtistThrottled = false;
    }

    public bool IsAlbumThrottled => _isAlbumThrottled;
    public bool IsArtistThrottled => _isArtistThrottled;
}
