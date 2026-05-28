using System.Text;
using Microsoft.Extensions.Logging;

namespace Rok.Infrastructure.Player.Streaming;

internal sealed class IcyStreamHandler : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;

    public event EventHandler<string>? MetadataChanged;

    public Stream AudioStream { get; private set; } = Stream.Null;
    public string? ContentType { get; private set; }

    public IcyStreamHandler(HttpClient httpClient, ILogger logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task ConnectAsync(string url, CancellationToken cancellationToken)
    {
        using HttpRequestMessage req = new(HttpMethod.Get, url);
        req.Headers.TryAddWithoutValidation("Icy-MetaData", "1");

        HttpResponseMessage resp = await _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        try
        {
            resp.EnsureSuccessStatusCode();
        }
        catch
        {
            resp.Dispose();
            throw;
        }

        ContentType = resp.Content.Headers.ContentType?.MediaType;

        int metaInt = ReadMetaInt(resp);

        Stream net = await resp.Content.ReadAsStreamAsync(cancellationToken);
        AudioStream = metaInt > 0
            ? new IcyDemuxStream(net, metaInt, OnMetadata, _logger)
            : net;
    }

    private static int ReadMetaInt(HttpResponseMessage resp)
    {
        if (resp.Headers.TryGetValues("icy-metaint", out IEnumerable<string>? values)
            && int.TryParse(values.First(), out int metaInt) && metaInt > 0)
            return metaInt;
        return 0;
    }

    private void OnMetadata(string title) => MetadataChanged?.Invoke(this, title);

    public void Dispose()
    {
        AudioStream.Dispose();
    }

    private sealed class IcyDemuxStream : Stream
    {
        private readonly Stream _source;
        private readonly int _metaInt;
        private readonly Action<string> _onMetadata;
        private readonly ILogger _logger;
        private int _bytesUntilMeta;
        private string? _lastTitle;

        public IcyDemuxStream(Stream source, int metaInt, Action<string> onMetadata, ILogger logger)
        {
            _source = source;
            _metaInt = metaInt;
            _bytesUntilMeta = metaInt;
            _onMetadata = onMetadata;
            _logger = logger;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => 0; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_bytesUntilMeta == 0)
            {
                ReadMetadataBlock();
                _bytesUntilMeta = _metaInt;
            }

            int toRead = Math.Min(count, _bytesUntilMeta);
            int read = _source.Read(buffer, offset, toRead);
            _bytesUntilMeta -= read;
            return read;
        }

        private void ReadMetadataBlock()
        {
            int lenByte = _source.ReadByte();
            if (lenByte <= 0) return;

            int len = lenByte * 16;
            byte[] block = new byte[len];
            int read = 0;
            while (read < len)
            {
                int n = _source.Read(block, read, len - read);
                if (n <= 0) break;
                read += n;
            }

            string text = Encoding.UTF8.GetString(block, 0, read).TrimEnd('\0');
            string? title = IcyMetadataParser.Parse(text);
            if (title is not null && title != _lastTitle)
            {
                _lastTitle = title;
                try { _onMetadata(title); }
                catch (Exception ex) { _logger.LogDebug(ex, "ICY metadata handler threw"); }
            }
        }
    }
}
