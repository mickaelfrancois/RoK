using Windows.System;

namespace Rok.Services.Accessibility;

public sealed record KeyboardShortcut(
    ShortcutId Id,
    ShortcutCategory Category,
    VirtualKeyModifiers Modifiers,
    VirtualKey Key,
    string LabelResourceKey);
