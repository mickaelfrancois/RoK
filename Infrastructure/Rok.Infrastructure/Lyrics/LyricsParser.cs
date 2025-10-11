using Rok.Application.Dto.Lyrics;
using Rok.Application.Interfaces;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Rok.Infrastructure.Lyrics;

public class LyricsParser : ILyricsParser
{
    private readonly Regex _lineRegex;


    public LyricsParser()
    {
        _lineRegex = new Regex(string.Format("\\{0}.*?\\{1}", '[', ']'), RegexOptions.Compiled, TimeSpan.FromSeconds(10));
    }


    public SyncLyricsModel Parse(string lyrics)
    {
        Guard.Against.NullOrEmpty(lyrics);

        SyncLyricsModel result = new();

        try
        {
            SortedDictionary<TimeSpan, string> sortedLyrics = [];

            foreach (string line in lyrics.Split(["\r\n", "\n", "\r"], StringSplitOptions.None))
            {
                string lyric = line.Substring(line.LastIndexOf(']') + 1);
                Match match = _lineRegex.Match(line);

                while (match.Success)
                {
                    TimeSpan time;
                    string item = match.Value;
                    item = item.Substring(1, item.Length - 2);

                    try
                    {
                        if (TimeSpan.TryParseExact(item, @"mm\:ss\.ff", CultureInfo.InvariantCulture, out time) == false)
                            TimeSpan.TryParseExact(item, @"mm\:ss", CultureInfo.InvariantCulture, out time);

                        if (time != TimeSpan.MinValue && lyric.Length > 0)
                        {
                            time = new TimeSpan(time.Hours, time.Minutes, time.Seconds);

                            sortedLyrics.Add(time, lyric);
                        }
                    }
                    catch
                    {
                        // Ignore
                    }

                    match = match.NextMatch();
                }
            }

            foreach (KeyValuePair<TimeSpan, string> item in sortedLyrics)
            {
                result.Time.Add(item.Key);
                result.Lyrics.Add(new LyricLine { Lyric = item.Value, Time = item.Key });
            }
        }
        catch
        {
            // Ignore
        }

        return result;
    }
}