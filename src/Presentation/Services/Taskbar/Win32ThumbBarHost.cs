using System.Runtime.InteropServices;
using Microsoft.Win32;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.System.Com;
using Windows.Win32.UI.Controls;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Rok.Services.Taskbar;

/// <summary>
/// Native host of the thumbnail toolbar: subclasses the main window, owns the <c>ITaskbarList3</c> and the icons.
/// Failures are logged and never propagated, so the taskbar can never take the UI down.
/// </summary>
public sealed unsafe class Win32ThumbBarHost : IThumbBarHost
{
    private const nuint SubclassId = 0x52544248;
    private const int ClickedNotification = 0x1800;
    private const string ImmersiveColorSet = "ImmersiveColorSet";
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string LightThemeValue = "SystemUsesLightTheme";
    private const int TooltipCapacity = 259;

    private readonly HWND _hwnd;
    private readonly ThumbBarLabels _labels;
    private readonly ILogger<Win32ThumbBarHost> _logger;
    private readonly SUBCLASSPROC _subclassProc;
    private readonly uint _taskbarButtonCreatedMessage;

    private ITaskbarList3? _taskbarList;
    private ThumbBarIconSet? _icons;
    private ThumbBarState _state = new(false, false, false, false);
    private bool _buttonsAdded;
    private bool _subclassInstalled;
    private bool _disposed;

    public Win32ThumbBarHost(nint windowHandle, ThumbBarLabels labels, ILogger<Win32ThumbBarHost> logger)
    {
        _hwnd = new HWND(windowHandle);
        _labels = labels;
        _logger = logger;
        _subclassProc = WindowProc;
        _taskbarButtonCreatedMessage = PInvoke.RegisterWindowMessage("TaskbarButtonCreated");

        try
        {
            _subclassInstalled = PInvoke.SetWindowSubclass(_hwnd, _subclassProc, SubclassId, 0);

            if (!_subclassInstalled)
                _logger.LogWarning("SetWindowSubclass failed, the taskbar thumbnail buttons are unavailable");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not subclass the main window, the taskbar thumbnail buttons are unavailable");
        }
    }

    public event EventHandler<ThumbBarButton>? ButtonClicked;

    public void Apply(ThumbBarState state)
    {
        if (_disposed)
            return;

        _state = state;

        if (!_buttonsAdded)
            return;

        UpdateButtons();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        RemoveSubclass();
        ReleaseTaskbarList();
        _icons?.Dispose();
        _icons = null;
    }

    private LRESULT WindowProc(HWND hwnd, uint message, WPARAM wParam, LPARAM lParam, nuint subclassId, nuint refData)
    {
        try
        {
            if (!_disposed && TryHandle(message, wParam, lParam))
                return new LRESULT(0);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unexpected failure while handling window message {Message}", message);
        }

        return PInvoke.DefSubclassProc(hwnd, message, wParam, lParam);
    }

    private bool TryHandle(uint message, WPARAM wParam, LPARAM lParam)
    {
        if (message == _taskbarButtonCreatedMessage)
        {
            OnTaskbarButtonCreated();

            return false;
        }

        switch (message)
        {
            case PInvoke.WM_COMMAND:
                return TryRaiseClick(wParam);

            case PInvoke.WM_SETTINGCHANGE:
                if (IsImmersiveColorSet(lParam))
                    RebuildIcons();

                return false;

            case PInvoke.WM_DPICHANGED:
                RebuildIcons();

                return false;

            case PInvoke.WM_NCDESTROY:
                RemoveSubclass();

                return false;

            default:
                return false;
        }
    }

    private bool TryRaiseClick(WPARAM wParam)
    {
        var value = (nuint)wParam;
        var notification = (int)((value >> 16) & 0xFFFF);
        var id = (int)(value & 0xFFFF);

        if (notification != ClickedNotification || id < (int)ThumbBarButton.Previous || id > (int)ThumbBarButton.Next)
            return false;

        ButtonClicked?.Invoke(this, (ThumbBarButton)id);

        return true;
    }

    private static bool IsImmersiveColorSet(LPARAM lParam)
    {
        if (lParam.Value == 0)
            return false;

        return Marshal.PtrToStringUni(lParam.Value) == ImmersiveColorSet;
    }

    private void OnTaskbarButtonCreated()
    {
        ReleaseTaskbarList();
        _buttonsAdded = false;

        try
        {
            PInvoke.CoCreateInstance<ITaskbarList3>(typeof(TaskbarList).GUID, null, CLSCTX.CLSCTX_INPROC_SERVER, out var list)
                .ThrowOnFailure();

            _taskbarList = list;
            _taskbarList.HrInit();

            _icons ??= BuildIconSet();

            if (_icons is null)
                return;

            AddButtons();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not create the taskbar thumbnail buttons");
        }
    }

    private void AddButtons()
    {
        if (_taskbarList is null || _icons is null)
            return;

        var buttons = BuildButtons(_icons, _state);

        fixed (THUMBBUTTON* pointer = buttons)
        {
            try
            {
                _taskbarList.ThumbBarAddButtons(_hwnd, (uint)buttons.Length, pointer);
                _buttonsAdded = true;
            }
            catch (COMException addFailure)
            {
                _logger.LogDebug(addFailure, "ThumbBarAddButtons refused, falling back to ThumbBarUpdateButtons");

                _taskbarList.ThumbBarUpdateButtons(_hwnd, (uint)buttons.Length, pointer);
                _buttonsAdded = true;
            }
        }
    }

    private bool UpdateButtons()
    {
        if (_taskbarList is null || _icons is null)
            return false;

        try
        {
            var buttons = BuildButtons(_icons, _state);

            fixed (THUMBBUTTON* pointer = buttons)
            {
                _taskbarList.ThumbBarUpdateButtons(_hwnd, (uint)buttons.Length, pointer);

                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not update the taskbar thumbnail buttons");

            return false;
        }
    }

    private void RebuildIcons()
    {
        var newIcons = BuildIconSet();

        if (newIcons is null)
            return;

        var oldIcons = _icons;
        _icons = newIcons;

        if (_buttonsAdded && !UpdateButtons())
        {
            _icons = oldIcons;
            newIcons.Dispose();

            return;
        }

        oldIcons?.Dispose();
    }

    private THUMBBUTTON[] BuildButtons(ThumbBarIconSet icons, ThumbBarState state)
    {
        return
        [
            BuildButton(ThumbBarButton.Previous, icons.Previous, _labels.Previous, state.IsPreviousEnabled),
            BuildButton(ThumbBarButton.PlayPause, state.ShowPause ? icons.Pause : icons.Play, state.ShowPause ? _labels.Pause : _labels.Play, state.IsPlayPauseEnabled),
            BuildButton(ThumbBarButton.Next, icons.Next, _labels.Next, state.IsNextEnabled)
        ];
    }

    private static THUMBBUTTON BuildButton(ThumbBarButton id, nint icon, string tooltip, bool enabled)
    {
        var button = new THUMBBUTTON
        {
            dwMask = THUMBBUTTONMASK.THB_ICON | THUMBBUTTONMASK.THB_TOOLTIP | THUMBBUTTONMASK.THB_FLAGS,
            iId = (uint)id,
            hIcon = new HICON((void*)icon),
            dwFlags = enabled ? THUMBBUTTONFLAGS.THBF_ENABLED : THUMBBUTTONFLAGS.THBF_DISABLED
        };

        var text = tooltip.Length > TooltipCapacity ? tooltip[..TooltipCapacity] : tooltip;
        var destination = button.szTip.AsSpan();
        text.AsSpan().CopyTo(destination);
        destination[text.Length] = '\0';

        return button;
    }

    private ThumbBarIconSet? BuildIconSet()
    {
        try
        {
            var dpi = PInvoke.GetDpiForWindow(_hwnd);
            var size = PInvoke.GetSystemMetricsForDpi(SYSTEM_METRICS_INDEX.SM_CXSMICON, dpi > 0 ? dpi : 96);

            if (size <= 0)
                size = 16;

            var color = ThumbBarPalette.GlyphColor(SystemUsesLightTheme());
            var previous = CreateIcon(ThumbBarGlyph.Previous, size, color);
            var play = CreateIcon(ThumbBarGlyph.Play, size, color);
            var pause = CreateIcon(ThumbBarGlyph.Pause, size, color);
            var next = CreateIcon(ThumbBarGlyph.Next, size, color);

            if (previous == 0 || play == 0 || pause == 0 || next == 0)
            {
                foreach (var handle in new[] { previous, play, pause, next })
                    DestroyIconHandle(handle);

                _logger.LogWarning("Could not create the taskbar thumbnail icons");

                return null;
            }

            return new ThumbBarIconSet(previous, play, pause, next, DestroyIconHandle);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not build the taskbar thumbnail icons");

            return null;
        }
    }

    private nint CreateIcon(ThumbBarGlyph glyph, int size, uint argb)
    {
        var pixels = ThumbBarIconRasterizer.Render(glyph, size, argb);
        var info = new BITMAPINFO();
        info.bmiHeader.biSize = (uint)sizeof(BITMAPINFOHEADER);
        info.bmiHeader.biWidth = size;
        info.bmiHeader.biHeight = -size;
        info.bmiHeader.biPlanes = 1;
        info.bmiHeader.biBitCount = 32;
        info.bmiHeader.biCompression = (uint)BI_COMPRESSION.BI_RGB;

        void* bits = null;
        var color = PInvoke.CreateDIBSection(default, &info, DIB_USAGE.DIB_RGB_COLORS, &bits, default, 0);
        var mask = default(HBITMAP);

        try
        {
            if (color.IsNull || bits is null)
            {
                _logger.LogWarning("CreateDIBSection failed for the {Glyph} icon", glyph);

                return 0;
            }

            pixels.AsSpan().CopyTo(new Span<uint>(bits, pixels.Length));

            mask = PInvoke.CreateBitmap(size, size, 1, 1, null);

            if (mask.IsNull)
            {
                _logger.LogWarning("CreateBitmap failed for the {Glyph} icon mask", glyph);

                return 0;
            }

            var iconInfo = new ICONINFO
            {
                fIcon = true,
                hbmColor = color,
                hbmMask = mask
            };

            var icon = PInvoke.CreateIconIndirect(iconInfo);

            if (icon.IsNull)
            {
                _logger.LogWarning("CreateIconIndirect failed for the {Glyph} icon", glyph);

                return 0;
            }

            return (nint)icon.Value;
        }
        finally
        {
            if (!mask.IsNull)
                PInvoke.DeleteObject(mask);

            if (!color.IsNull)
                PInvoke.DeleteObject(color);
        }
    }

    private void DestroyIconHandle(nint handle)
    {
        if (handle == 0)
            return;

        if (!PInvoke.DestroyIcon(new HICON((void*)handle)))
            _logger.LogWarning("DestroyIcon failed for handle {Handle}", handle);
    }

    private static bool SystemUsesLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);

            return key?.GetValue(LightThemeValue) is int value && value != 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private void RemoveSubclass()
    {
        if (!_subclassInstalled)
            return;

        _subclassInstalled = false;

        try
        {
            PInvoke.RemoveWindowSubclass(_hwnd, _subclassProc, SubclassId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not remove the main window subclass");
        }
    }

    private void ReleaseTaskbarList()
    {
        var list = _taskbarList;
        _taskbarList = null;

        if (list is null)
            return;

        try
        {
            Marshal.FinalReleaseComObject(list);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not release the taskbar COM object");
        }
    }
}