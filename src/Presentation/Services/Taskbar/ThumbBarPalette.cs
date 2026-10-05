namespace Rok.Services.Taskbar;

/// <summary>Picks the glyph color that contrasts with the taskbar theme.</summary>
public static class ThumbBarPalette
{
    private const uint DarkGlyph = 0xFF1F1F1F;
    private const uint LightGlyph = 0xFFFFFFFF;

    public static uint GlyphColor(bool systemUsesLightTheme) => systemUsesLightTheme ? DarkGlyph : LightGlyph;
}