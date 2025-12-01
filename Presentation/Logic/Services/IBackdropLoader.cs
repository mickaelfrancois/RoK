namespace Rok.Logic.Services;

public interface IBackdropLoader
{
    void LoadBackdrop(string artistName, Action<BitmapImage?> setBackdrop);
}
