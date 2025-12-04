using Rok.Application.Dto.Lyrics;
using Rok.Application.Interfaces;
using System.Text.RegularExpressions;

namespace Rok.Infrastructure.Lyrics;

public class LyricsService : ILyricsService
{
    private static readonly string[] separator = new[] { "\r\n", "\n", "\r" };

    public string GetSynchronizedLyricsFileName(string musicFile)
    {
        Guard.Against.NullOrEmpty(musicFile, nameof(musicFile));

        string? folder = Path.GetDirectoryName(musicFile);
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(musicFile);

        return Path.Combine(folder!, fileNameWithoutExtension + ".lrc");
    }

    public string GetPlainLyricsFileName(string musicFile)
    {
        Guard.Against.NullOrEmpty(musicFile, nameof(musicFile));

        string? folder = Path.GetDirectoryName(musicFile);
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(musicFile);

        return Path.Combine(folder!, fileNameWithoutExtension + ".txt");
    }


    public ELyricsType CheckLyricsFileExists(string musicFile)
    {
        Guard.Against.NullOrEmpty(musicFile, nameof(musicFile));

        string? folder = Path.GetDirectoryName(musicFile);
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(musicFile);
        string lyricsLrc = Path.Combine(folder!, fileNameWithoutExtension + ".lrc");
        string lyricsTxt = Path.Combine(folder!, fileNameWithoutExtension + ".txt");

        if (File.Exists(lyricsLrc))
            return ELyricsType.Synchronized;

        if (File.Exists(lyricsTxt))
            return ELyricsType.Plain;

        return ELyricsType.None;
    }


    public async Task<LyricsModel?> LoadLyricsAsync(string musicFile)
    {
        Guard.Against.NullOrEmpty(musicFile, nameof(musicFile));

        string? folder = Path.GetDirectoryName(musicFile);

        if (string.IsNullOrEmpty(folder))
            return null;

        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(musicFile);
        string lyricsLrc = Path.Combine(folder, fileNameWithoutExtension + ".lrc");
        string lyricsTxt = Path.Combine(folder, fileNameWithoutExtension + ".txt");

        if (File.Exists(lyricsLrc))
        {
            LyricsModel lyrics = new()
            {
                File = lyricsLrc,
                SynchronizedLyrics = await File.ReadAllTextAsync(lyricsLrc),
                LyricsType = ELyricsType.Synchronized
            };

            lyrics.PlainLyrics = GetRawLyrics(lyrics.SynchronizedLyrics);

            return lyrics;
        }

        if (File.Exists(lyricsTxt))
        {
            LyricsModel lyrics = new()
            {
                File = lyricsTxt,
                PlainLyrics = await File.ReadAllTextAsync(lyricsTxt),
                LyricsType = ELyricsType.Plain
            };

            return lyrics;
        }

        return null;
    }


    public async Task SaveLyricsAsync(LyricsModel lyrics)
    {
        await File.WriteAllTextAsync(lyrics.File, lyrics.PlainLyrics);
    }


    public static string GetRawLyrics(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        Regex regex = new(@"\[[^\]]*\]", RegexOptions.Compiled);
        string[] lines = text.Split(separator, StringSplitOptions.None);

        for (int i = 0; i < lines.Length; i++)
        {
            lines[i] = regex.Replace(lines[i], string.Empty).Trim();
        }

        int start = 0;
        int end = lines.Length - 1;

        while (start <= end && string.IsNullOrWhiteSpace(lines[start]))
            start++;

        while (end >= start && string.IsNullOrWhiteSpace(lines[end]))
            end--;

        if (start > end)
            return string.Empty;

        return string.Join(Environment.NewLine, lines[start..(end + 1)]);
    }
}