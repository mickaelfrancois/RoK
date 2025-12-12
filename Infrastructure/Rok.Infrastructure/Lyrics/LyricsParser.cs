using Rok.Application.Dto.Lyrics;
using Rok.Application.Interfaces;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Rok.Infrastructure.Lyrics;

public partial class LyricsParser : ILyricsParser
{
    private static readonly string[] LineSeparators = ["\r\n", "\n", "\r"];
    private static readonly string[] TimeFormats = [@"mm\:ss\.ff", @"mm\:ss"];

    [GeneratedRegex(@"\[.*?\]", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TimestampPattern();

    public SyncLyricsModel Parse(string lyrics)
    {
        Guard.Against.NullOrEmpty(lyrics);

        SyncLyricsModel result = new();
        SortedDictionary<TimeSpan, string> sortedLyrics = new();

        string[] lines = lyrics.Split(LineSeparators, StringSplitOptions.None);

        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            ProcessLine(line, sortedLyrics);
        }

        PopulateResult(sortedLyrics, result);

        return result;
    }

    private static void ProcessLine(string line, SortedDictionary<TimeSpan, string> sortedLyrics)
    {
        int lastBracketIndex = line.LastIndexOf(']');
        if (lastBracketIndex < 0)
            return;

        string lyric = line[(lastBracketIndex + 1)..];

        if (string.IsNullOrWhiteSpace(lyric))
            return;

        MatchCollection matches = TimestampPattern().Matches(line);
        bool hasValidTimestamp = false;

        foreach (Match match in matches)
        {
            if (TryParseTimestamp(match.Value, out TimeSpan time))
            {
                hasValidTimestamp = true;
                TimeSpan normalizedTime = new(0, time.Minutes, time.Seconds);

                if (!sortedLyrics.ContainsKey(normalizedTime))
                {
                    sortedLyrics.Add(normalizedTime, lyric);
                }
            }
        }

        if (!hasValidTimestamp && matches.Count > 0 && !sortedLyrics.ContainsKey(TimeSpan.Zero))
        {
            sortedLyrics.Add(TimeSpan.Zero, lyric);
        }
    }

    private static bool TryParseTimestamp(string timestampWithBrackets, out TimeSpan time)
    {
        time = TimeSpan.Zero;

        if (timestampWithBrackets.Length < 3)
            return false;

        string timestamp = timestampWithBrackets.Substring(1, timestampWithBrackets.Length - 2);

        foreach (string format in TimeFormats)
        {
            if (TimeSpan.TryParseExact(timestamp, format, CultureInfo.InvariantCulture, out time))
            {
                return time != TimeSpan.Zero;
            }
        }

        return false;
    }

    private static void PopulateResult(SortedDictionary<TimeSpan, string> sortedLyrics, SyncLyricsModel result)
    {
        foreach (KeyValuePair<TimeSpan, string> item in sortedLyrics)
        {
            result.Time.Add(item.Key);
            result.Lyrics.Add(new LyricLine
            {
                Lyric = item.Value,
                Time = item.Key
            });
        }
    }
}