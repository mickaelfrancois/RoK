namespace Rok.Services;

public interface IBackdropLoader
{
    void LoadBackdrop(string artistName, Action<BitmapImage?> setBackdrop);
}
