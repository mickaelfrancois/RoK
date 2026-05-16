using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Rok.Services.Accessibility;
using Windows.Foundation;

namespace Rok.Services;

public sealed class KeyboardShortcutInstaller
{
    public KeyboardAccelerator Build(ShortcutId id, TypedEventHandler<KeyboardAccelerator, KeyboardAcceleratorInvokedEventArgs> handler)
    {
        KeyboardShortcut shortcut = KeyboardShortcutCatalog.ById(id);

        KeyboardAccelerator accelerator = new()
        {
            Key = shortcut.Key,
            Modifiers = shortcut.Modifiers
        };

        accelerator.Invoked += handler;

        return accelerator;
    }
}