namespace Rok.Application.Dto;

public static class EqualizerBuiltinPresets
{
    public static readonly EqualizerBuiltinPreset Flat = new("Flat", new float[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
    public static readonly EqualizerBuiltinPreset Rock = new("Rock", new float[] { 5, 4, 3, 1, -1, -1, 1, 3, 4, 5 });
    public static readonly EqualizerBuiltinPreset Metal = new("Metal", new float[] { 7, 4, 3, 3, -2, -2, 1, 4, 6, 7 });
    public static readonly EqualizerBuiltinPreset Pop = new("Pop", new float[] { -1, -1, 0, 2, 5, 5, 3, -2, -2, -1 });
    public static readonly EqualizerBuiltinPreset HipHop = new("HipHop", new float[] { 5, 4, 2, 3, -1, -1, 0, 0, 2, 3 });
    public static readonly EqualizerBuiltinPreset RnB = new("RnB", new float[] { 6, 5, 3, 2, 0, -1, 1, 2, 3, 4 });
    public static readonly EqualizerBuiltinPreset Electronic = new("Electronic", new float[] { 6, 5, 3, 1, 0, -1, 1, 3, 4, 5 });
    public static readonly EqualizerBuiltinPreset Classical = new("Classical", new float[] { 3, 3, 2, 1, 0, 0, 0, 1, 2, 3 });
    public static readonly EqualizerBuiltinPreset Jazz = new("Jazz", new float[] { 4, 3, 2, 2, -1, -1, 1, 2, 3, 3 });
    public static readonly EqualizerBuiltinPreset Blues = new("Blues", new float[] { 4, 3, 3, 2, 1, 0, 1, 2, 3, 3 });
    public static readonly EqualizerBuiltinPreset Acoustic = new("Acoustic", new float[] { 3, 2, 2, 3, 2, 1, 1, 2, 2, 3 });
    public static readonly EqualizerBuiltinPreset Vocal = new("Vocal", new float[] { -2, -2, 0, 2, 4, 5, 4, 2, 1, 0 });
    public static readonly EqualizerBuiltinPreset BassBoost = new("BassBoost", new float[] { 7, 7, 5, 2, 0, 0, 0, 0, 0, 0 });
    public static readonly EqualizerBuiltinPreset TrebleBoost = new("TrebleBoost", new float[] { 0, 0, 0, 0, 0, 1, 3, 5, 6, 7 });
    public static readonly EqualizerBuiltinPreset Loudness = new("Loudness", new float[] { 6, 5, 0, -1, -1, 0, 1, 3, 5, 6 });
    public static readonly EqualizerBuiltinPreset Latin = new("Latin", new float[] { 5, 4, 2, 1, 0, -1, 1, 2, 4, 5 });

    public static readonly IReadOnlyList<EqualizerBuiltinPreset> All = new List<EqualizerBuiltinPreset>
    {
        Flat, Rock, Metal, Pop, HipHop, RnB, Electronic, Classical, Jazz, Blues, Acoustic, Vocal, BassBoost, TrebleBoost, Loudness, Latin
    };
}
