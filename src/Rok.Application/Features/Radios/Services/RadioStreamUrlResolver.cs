using System.Net.Http.Headers;
using System.Text;
using CleanArch.DevKit.Mediator.Results;
using Rok.Application.Errors;

namespace Rok.Application.Features.Radios.Services;

public class RadioStreamUrlResolver(HttpClient httpClient) : IRadioStreamUrlResolver
{
    private const long MaxPlaylistBytes = 1024 * 1024;

    public async Task<Result<string>> ResolveAsync(string url, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
            return Result<string>.Fail(new OperationError("radio.invalid_url", "Invalid URL."));

        HttpResponseMessage response;
        try
        {
            response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            return Result<string>.Fail(new OperationError("radio.fetch_failed", ex.Message));
        }

        if (!response.IsSuccessStatusCode)
            return Result<string>.Fail(new OperationError("radio.fetch_failed", $"HTTP {(int)response.StatusCode}"));

        MediaTypeHeaderValue? mediaType = response.Content.Headers.ContentType;
        string mime = mediaType?.MediaType?.ToLowerInvariant() ?? string.Empty;

        if (IsDirectAudio(mime, uri))
            return Result<string>.Ok(uri.ToString());

        if (!IsPlaylist(mime, uri))
            return Result<string>.Fail(new OperationError("radio.unsupported_format", $"Unsupported content type '{mime}'."));

        string body = await ReadBodyAsync(response, cancellationToken);

        if (IsHlsManifest(body))
            return Result<string>.Fail(new OperationError("radio.hls_unsupported", "HLS streams are not supported."));

        string? extracted = ExtractStreamUrl(body, mime, uri);
        if (string.IsNullOrEmpty(extracted))
            return Result<string>.Fail(new OperationError("radio.no_stream_in_playlist", "No usable stream URL found in playlist."));

        return Result<string>.Ok(extracted);
    }

    private static bool IsDirectAudio(string mime, Uri uri)
    {
        if (mime.StartsWith("audio/", StringComparison.Ordinal)
            && !IsPlaylist(mime, uri))
            return true;

        return false;
    }

    private static bool IsPlaylist(string mime, Uri uri)
    {
        if (mime is "audio/x-mpegurl" or "audio/mpegurl" or "audio/x-scpls" or "application/vnd.apple.mpegurl")
            return true;

        string path = uri.AbsolutePath.ToLowerInvariant();
        return path.EndsWith(".pls", StringComparison.Ordinal)
            || path.EndsWith(".m3u", StringComparison.Ordinal)
            || path.EndsWith(".m3u8", StringComparison.Ordinal);
    }

    private static bool IsHlsManifest(string body) =>
        body.Contains("#EXT-X-TARGETDURATION", StringComparison.Ordinal)
        || body.Contains("#EXT-X-STREAM-INF", StringComparison.Ordinal)
        || body.Contains("#EXT-X-VERSION", StringComparison.Ordinal);

    private static string? ExtractStreamUrl(string body, string mime, Uri uri)
    {
        bool isPls = mime == "audio/x-scpls"
                  || uri.AbsolutePath.EndsWith(".pls", StringComparison.OrdinalIgnoreCase);

        if (isPls)
            return ExtractFromPls(body);

        return ExtractFromM3u(body);
    }

    private static string? ExtractFromPls(string body)
    {
        foreach (string raw in body.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string line = raw.Trim();
            if (!line.StartsWith("File", StringComparison.OrdinalIgnoreCase))
                continue;

            int eq = line.IndexOf('=');
            if (eq <= 0 || eq >= line.Length - 1)
                continue;

            string value = line[(eq + 1)..].Trim();
            if (Uri.TryCreate(value, UriKind.Absolute, out _))
                return value;
        }

        return null;
    }

    private static string? ExtractFromM3u(string body)
    {
        foreach (string raw in body.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            if (Uri.TryCreate(line, UriKind.Absolute, out _))
                return line;
        }

        return null;
    }

    private static async Task<string> ReadBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using StreamReader reader = new(stream, Encoding.UTF8);
        char[] buffer = new char[1024];
        StringBuilder sb = new();
        long total = 0;

        while (true)
        {
            int read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (read == 0) break;
            sb.Append(buffer, 0, read);
            total += read;
            if (total >= MaxPlaylistBytes) break;
        }

        return sb.ToString();
    }
}