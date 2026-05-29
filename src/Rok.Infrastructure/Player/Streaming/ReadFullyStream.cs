namespace Rok.Infrastructure.Player.Streaming;

/// <summary>
/// Wraps a non-seekable stream and retries partial reads until the requested
/// byte count is satisfied or the underlying stream reaches EOF. This is
/// required by <see cref="NAudio.Wave.Mp3Frame.LoadFromStream"/> which expects
/// each read to return exactly the requested number of bytes.
/// </summary>
internal sealed class ReadFullyStream(Stream source) : Stream
{
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => 0; set { } }

    public override void Flush() { }

    public override long Seek(long offset, SeekOrigin origin) =>
        throw new NotSupportedException();

    public override void SetLength(long value) =>
        throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    public override int Read(byte[] buffer, int offset, int count)
    {
        int totalRead = 0;

        while (totalRead < count)
        {
            int read = source.Read(buffer, offset + totalRead, count - totalRead);

            if (read <= 0)
                break;

            totalRead += read;
        }

        return totalRead;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            source.Dispose();

        base.Dispose(disposing);
    }
}