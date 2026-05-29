using Microsoft.Extensions.Logging;

namespace Rok.Infrastructure.Player.Streaming;

/// <summary>
/// Wraps a non-seekable live network stream so that <c>StreamMediaFoundationReader</c>
/// can probe the container during construction. Media Foundation issues
/// arbitrary seeks while sniffing the header (typically scanning the first
/// several MB), then rewinds before decoding. The wrapper grows an in-memory
/// buffer up to <see cref="MaxBufferBytes"/>; rewinds inside the buffer are
/// free, forward seeks read-and-discard, and backward seeks outside the
/// buffered region throw with a diagnostic log.
/// </summary>
internal sealed class MediaFoundationSeekableStream : Stream
{
    private const int InitialBufferSize = 64 * 1024;
    private const int MaxBufferBytes = 16 * 1024 * 1024;
    private const int ForwardSkipChunk = 8192;

    private readonly Stream _source;
    private readonly ILogger _logger;
    private byte[] _buffer = new byte[InitialBufferSize];
    private int _bufferedBytes;
    private long _position;

    public MediaFoundationSeekableStream(Stream source, ILogger logger)
    {
        _source = source;
        _logger = logger;
    }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;

    public override long Length => long.MaxValue;

    public override long Position
    {
        get => _position;
        set => SetPosition(value);
    }

    public override void Flush() { }

    public override long Seek(long offset, SeekOrigin origin)
    {
        long target = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => long.MaxValue,
            _ => throw new ArgumentOutOfRangeException(nameof(origin))
        };

        SetPosition(target);
        return _position;
    }

    public override void SetLength(long value) =>
        throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_position < _bufferedBytes)
        {
            int available = _bufferedBytes - (int)_position;
            int fromBuffer = Math.Min(count, available);
            Array.Copy(_buffer, (int)_position, buffer, offset, fromBuffer);
            _position += fromBuffer;
            return fromBuffer;
        }

        int read = _source.Read(buffer, offset, count);

        if (read > 0 && _bufferedBytes < MaxBufferBytes)
        {
            int room = MaxBufferBytes - _bufferedBytes;
            int toCopy = Math.Min(room, read);

            EnsureBufferCapacity(_bufferedBytes + toCopy);
            Array.Copy(buffer, offset, _buffer, _bufferedBytes, toCopy);
            _bufferedBytes += toCopy;
        }

        _position += read;
        return read;
    }

    private void SetPosition(long value)
    {
        if (value == _position)
            return;

        if (value >= 0 && value <= _bufferedBytes)
        {
            _position = value;
            return;
        }

        if (value > _position)
        {
            long toSkip = value - _position;

            if (toSkip > MaxBufferBytes)
            {
                _logger.LogWarning(
                    "Refusing forward seek of {Bytes} bytes (target {Target}, current {Current}) — exceeds {Max}",
                    toSkip, value, _position, MaxBufferBytes);
                throw new NotSupportedException(
                    $"Forward seek of {toSkip} bytes exceeds buffer limit.");
            }

            _logger.LogDebug("Forward seek by {Bytes} bytes (target {Target})", toSkip, value);

            byte[] discard = new byte[ForwardSkipChunk];
            while (toSkip > 0)
            {
                int chunk = (int)Math.Min(toSkip, discard.Length);
                int read = Read(discard, 0, chunk);
                if (read <= 0)
                {
                    _logger.LogWarning("Source returned EOF during forward seek (remaining {Bytes})", toSkip);
                    break;
                }
                toSkip -= read;
            }
            return;
        }

        _logger.LogWarning(
            "Backward seek to {Target} not supported (buffered {Buffered}, current {Current})",
            value, _bufferedBytes, _position);
        throw new NotSupportedException(
            $"Backward seek to {value} beyond buffered region ({_bufferedBytes} bytes).");
    }

    private void EnsureBufferCapacity(int required)
    {
        if (_buffer.Length >= required)
            return;

        int newSize = _buffer.Length;

        while (newSize < required)
            newSize *= 2;

        if (newSize > MaxBufferBytes)
            newSize = MaxBufferBytes;

        Array.Resize(ref _buffer, newSize);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _source.Dispose();

        base.Dispose(disposing);
    }
}