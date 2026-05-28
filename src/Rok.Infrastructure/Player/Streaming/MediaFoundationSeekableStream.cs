namespace Rok.Infrastructure.Player.Streaming;

/// <summary>
/// Wraps a non-seekable network stream so that <c>MediaFoundationReader</c> /
/// <c>StreamMediaFoundationReader</c> can probe the format. Media Foundation
/// reads the first bytes of the stream to identify the container, then rewinds
/// (Position = 0) before starting actual decoding. The first
/// <see cref="EarlyBufferLimit"/> bytes are mirrored into an in-memory buffer
/// so a rewind into that region is supported; reads past it remain
/// forward-only.
/// </summary>
internal sealed class MediaFoundationSeekableStream : Stream
{
    private const int EarlyBufferLimit = 64 * 1024;

    private readonly Stream _source;
    private readonly byte[] _earlyBuffer = new byte[EarlyBufferLimit];
    private int _earlyBufferLength;
    private long _position;

    public MediaFoundationSeekableStream(Stream source)
    {
        _source = source;
    }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;

    public override long Length => long.MaxValue;

    public override long Position
    {
        get => _position;
        set
        {
            if (value == _position)
                return;

            if (value >= 0 && value <= _earlyBufferLength)
            {
                _position = value;
                return;
            }

            throw new NotSupportedException(
                "Live stream can only rewind within its initial probe buffer.");
        }
    }

    public override void Flush() { }

    public override long Seek(long offset, SeekOrigin origin)
    {
        long target = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => throw new NotSupportedException(),
            _ => throw new ArgumentOutOfRangeException(nameof(origin))
        };

        Position = target;
        return _position;
    }

    public override void SetLength(long value) =>
        throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_position < _earlyBufferLength)
        {
            int available = _earlyBufferLength - (int)_position;
            int fromBuffer = Math.Min(count, available);
            Array.Copy(_earlyBuffer, (int)_position, buffer, offset, fromBuffer);
            _position += fromBuffer;
            return fromBuffer;
        }

        int read = _source.Read(buffer, offset, count);

        if (_earlyBufferLength < EarlyBufferLimit && read > 0)
        {
            int room = EarlyBufferLimit - _earlyBufferLength;
            int toCopy = Math.Min(room, read);
            Array.Copy(buffer, offset, _earlyBuffer, _earlyBufferLength, toCopy);
            _earlyBufferLength += toCopy;
        }

        _position += read;
        return read;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _source.Dispose();

        base.Dispose(disposing);
    }
}
