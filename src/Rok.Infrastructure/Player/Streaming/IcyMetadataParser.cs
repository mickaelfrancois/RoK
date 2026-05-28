namespace Rok.Infrastructure.Player.Streaming;

internal static class IcyMetadataParser
{
    private const string Prefix = "StreamTitle='";

    public static string? Parse(string block)
    {
        if (string.IsNullOrEmpty(block))
            return null;

        int start = block.IndexOf(Prefix, StringComparison.Ordinal);
        if (start < 0)
            return null;

        int valueStart = start + Prefix.Length;
        int valueEnd = block.IndexOf('\'', valueStart);
        if (valueEnd < 0)
            return null;

        return block.Substring(valueStart, valueEnd - valueStart);
    }
}
