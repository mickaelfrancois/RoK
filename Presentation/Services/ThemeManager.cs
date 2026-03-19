using Microsoft.UI;
using Microsoft.UI.Windowing;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace Rok.Services;

public static class ThemeManager
{
    private static UISettings? _uiSettings;
    private static AppTheme _currentChoice = AppTheme.System;
    private static bool _initialized;
    private static Window? _window;


    public static void Initialize(AppTheme initialChoice, Window window)
    {
        if (_initialized)
            return;

        _initialized = true;

        _window = window ?? throw new ArgumentNullException(nameof(window));

        _uiSettings = new UISettings();
        _uiSettings.ColorValuesChanged += UiSettings_ColorValuesChanged;

        Apply(initialChoice);
    }


    public static void Toggle()
    {
        if (_currentChoice == AppTheme.Light)
            Apply(AppTheme.Dark);
        else
            Apply(AppTheme.Light);
    }


    public static void AttachWindow(Window window)
    {
        _window = window;
        Apply(_currentChoice);
    }


    private static void UiSettings_ColorValuesChanged(UISettings sender, object args)
    {
        if (_currentChoice != AppTheme.System)
            return;

        _window?.DispatcherQueue.TryEnqueue(() => Apply(AppTheme.System));
    }


    public static void Apply(AppTheme choice)
    {
        _currentChoice = choice;

        ElementTheme effective = choice switch
        {
            AppTheme.Light => ElementTheme.Light,
            AppTheme.Dark => ElementTheme.Dark,
            AppTheme.System => GetSystemElementTheme(),
            _ => ElementTheme.Default
        };

        if (_window?.Content is FrameworkElement fe)
            fe.RequestedTheme = effective;

        ApplyTitleBarColors();
    }


    private static ElementTheme GetSystemElementTheme()
    {
        if (_uiSettings == null)
            _uiSettings = new UISettings();

        // Heuristics: Light background => Light, otherwise Dark
        Color bg = _uiSettings.GetColorValue(UIColorType.Background);

        return IsLight(bg) ? ElementTheme.Light : ElementTheme.Dark;
    }


    private static bool IsLight(Color c)
    {
        // Simple relative luminance
        double luminance = ((0.2126 * c.R) + (0.7152 * c.G) + (0.0722 * c.B)) / 255.0;
        return luminance >= 0.5;
    }


    private static void ApplyTitleBarColors()
    {
        if (_window == null || !AppWindowTitleBar.IsCustomizationSupported())
            return;

        AppWindowTitleBar titleBar = _window.AppWindow.TitleBar;

        Color brand = Color.FromArgb(0xFF, 0x15, 0x45, 0x87); // Blue700
        Color hover = Color.FromArgb(0xFF, 0x25, 0x7E, 0xF9); // Blue400
        Color pressed = Color.FromArgb(0xFF, 0x12, 0x3D, 0x7A); // Blue800
        Color inactive = Color.FromArgb(0xFF, 0x0D, 0x2A, 0x54); // Blue900
        Color dimText = Color.FromArgb(0xFF, 0xAA, 0xAA, 0xAA);

        titleBar.BackgroundColor = brand;
        titleBar.ForegroundColor = Colors.White;
        titleBar.InactiveBackgroundColor = inactive;
        titleBar.InactiveForegroundColor = dimText;

        titleBar.ButtonBackgroundColor = brand;
        titleBar.ButtonForegroundColor = Colors.White;
        titleBar.ButtonHoverBackgroundColor = hover;
        titleBar.ButtonHoverForegroundColor = Colors.White;
        titleBar.ButtonPressedBackgroundColor = pressed;
        titleBar.ButtonPressedForegroundColor = Colors.White;
        titleBar.ButtonInactiveBackgroundColor = inactive;
        titleBar.ButtonInactiveForegroundColor = dimText;
    }
}