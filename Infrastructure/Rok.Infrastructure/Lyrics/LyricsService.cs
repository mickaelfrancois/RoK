using Rok.Application.Dto.Lyrics;
using Rok.Application.Interfaces;
using System.Text.RegularExpressions;

namespace Rok.Infrastructure.Lyrics;

public partial class LyricsService(IFileSystem fileSystem) : ILyricsService
{
    private static readonly string[] LineSeparators = ["\r\n", "\n", "\r"];

    [GeneratedRegex(@"\[[^\]]*\]", RegexOptions.Compiled, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TimestampRegex();

    public string GetSynchronizedLyricsFileName(string musicFile)
    {
        Guard.Against.NullOrEmpty(musicFile);

        string? folder = fileSystem.GetDirectoryName(musicFile);
        string fileNameWithoutExtension = fileSystem.GetFileNameWithoutExtension(musicFile);

        return fileSystem.Combine(folder!, fileNameWithoutExtension + ".lrc");
    }

    public string GetPlainLyricsFileName(string musicFile)
    {
        Guard.Against.NullOrEmpty(musicFile);

        string? folder = fileSystem.GetDirectoryName(musicFile);
        string fileNameWithoutExtension = fileSystem.GetFileNameWithoutExtension(musicFile);

        return fileSystem.Combine(folder!, fileNameWithoutExtension + ".txt");
    }

    public ELyricsType CheckLyricsFileExists(string musicFile)
    {
        Guard.Against.NullOrEmpty(musicFile);

        string? folder = fileSystem.GetDirectoryName(musicFile);
        string fileNameWithoutExtension = fileSystem.GetFileNameWithoutExtension(musicFile);
        string lyricsLrc = fileSystem.Combine(folder!, fileNameWithoutExtension + ".lrc");
        string lyricsTxt = fileSystem.Combine(folder!, fileNameWithoutExtension + ".txt");

        if (fileSystem.FileExists(lyricsLrc))
            return ELyricsType.Synchronized;

        if (fileSystem.FileExists(lyricsTxt))
            return ELyricsType.Plain;

        return ELyricsType.None;
    }

    public async Task<LyricsModel?> LoadLyricsAsync(string musicFile)
    {
        Guard.Against.NullOrEmpty(musicFile);

        string? folder = fileSystem.GetDirectoryName(musicFile);

        if (string.IsNullOrEmpty(folder))
            return null;

        string fileNameWithoutExtension = fileSystem.GetFileNameWithoutExtension(musicFile);
        string lyricsLrc = fileSystem.Combine(folder, fileNameWithoutExtension + ".lrc");
        string lyricsTxt = fileSystem.Combine(folder, fileNameWithoutExtension + ".txt");

        if (fileSystem.FileExists(lyricsLrc))
        {
            string content = await fileSystem.ReadAllTextAsync(lyricsLrc);

            return new LyricsModel
            {
                File = lyricsLrc,
                SynchronizedLyrics = content,
                LyricsType = ELyricsType.Synchronized,
                PlainLyrics = GetRawLyrics(content)
            };
        }

        if (fileSystem.FileExists(lyricsTxt))
        {
            return new LyricsModel
            {
                File = lyricsTxt,
                PlainLyrics = await fileSystem.ReadAllTextAsync(lyricsTxt),
                LyricsType = ELyricsType.Plain
            };
        }

        return null;
    }

    public async Task SaveLyricsAsync(LyricsModel lyrics)
    {
        Guard.Against.Null(lyrics);

        await fileSystem.WriteAllTextAsync(lyrics.File, lyrics.PlainLyrics);
    }

    public static string GetRawLyrics(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        Regex regex = TimestampRegex();
        string[] lines = text.Split(LineSeparators, StringSplitOptions.None);

        for (int i = 0; i < lines.Length; i++)
        {
            lines[i] = regex.Replace(lines[i], string.Empty).Trim();
        }

        int start = FindFirstNonEmptyLine(lines);
        int end = FindLastNonEmptyLine(lines);

        if (start > end)
            return string.Empty;

        return string.Join(Environment.NewLine, lines[start..(end + 1)]);
    }

    private static int FindFirstNonEmptyLine(string[] lines)
    {
        for (int i = 0; i < lines.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(lines[i]))
                return i;
        }

        return lines.Length;
    }

    private static int FindLastNonEmptyLine(string[] lines)
    {
        for (int i = lines.Length - 1; i >= 0; i--)
        {
            if (!string.IsNullOrWhiteSpace(lines[i]))
                return i;
        }

        return -1;
    }
}