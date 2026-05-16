using Rok.Services;
using Rok.ViewModels.Player;

namespace Rok.Commons.Equalizer;

internal class EqualizerWindowService(EqualizerViewModel equalizerViewModel, ResourceLoader resourceLoader) : IEqualizerWindowService
{
    private EqualizerWindow? _window;

    public void ShowOrActivate()
    {
        if (_window != null)
        {
            _window.Activate();
            return;
        }

        _window = new EqualizerWindow(equalizerViewModel, resourceLoader);
        _window.Closed += (_, _) => _window = null;
        _window.Activate();
    }

    public void Close()
    {
        _window?.Close();
        _window = null;
    }
}