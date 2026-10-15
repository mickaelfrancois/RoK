using System.Buffers.Binary;
using System.Text;

namespace Rok.Infrastructure.Tag.Formats;

/// <summary>
/// A FLAC metadata block as stored in the file.
/// </summary>
/// <param name="Type">Block type (0 STREAMINFO, 1 PADDING, 4 VORBIS_COMMENT, 6 PICTURE...).</param>
/// <param name="Data">Raw block body, without its 4-byte header.</param>
internal sealed record FlacMetadataBlock(byte Type, byte[] Data);

/// <summary>
/// Structure of a FLAC file: where the audio frames start, the metadata blocks and the raw Vorbis comment fields.
/// </summary>
/// <param name="FirstFrameOffset">Position of the first audio frame.</param>
/// <param name="Blocks">Metadata blocks in file order.</param>
/// <param name="VorbisFields">Raw <c>NAME=value</c> entries of the VORBIS_COMMENT block, vendor excluded.</param>
internal sealed record FlacLayoutInfo(long FirstFrameOffset, IReadOnlyList<FlacMetadataBlock> Blocks, IReadOnlyList<string> VorbisFields);

/// <summary>
/// Minimal FLAC parser that locates the audio frames without relying on TagLib's invariant positions.
/// </summary>
internal static class FlacLayout
{
    /// <summary>Block type of VORBIS_COMMENT.</summary>
    public const byte VorbisCommentType = 4;

    /// <summary>Block type of PADDING.</summary>
    public const byte PaddingType = 1;

    private const int Id3v2HeaderSize = 10;
    private const int Id3v2FooterFlag = 0x10;
    private const int BlockHeaderSize = 4;

    private static readonly byte[] Marker = "fLaC"u8.ToArray();

    /// <summary>
    /// Reads the layout of a FLAC file.
    /// </summary>
    /// <param name="path">File to read.</param>
    /// <returns>The layout.</returns>
    /// <exception cref="TagLib.CorruptFileException">The file is not a well-formed FLAC stream.</exception>
    public static FlacLayoutInfo Read(string path)
    {
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);

        return Read(stream);
    }

    /// <summary>
    /// Reads the layout of a FLAC stream.
    /// </summary>
    /// <param name="stream">Seekable stream positioned anywhere.</param>
    /// <returns>The layout.</returns>
    /// <exception cref="TagLib.CorruptFileException">The stream is not a well-formed FLAC stream.</exception>
    public static FlacLayoutInfo Read(Stream stream)
    {
        long position = SkipLeadingId3v2(stream);

        stream.Seek(position, SeekOrigin.Begin);

        byte[] marker = ReadExactly(stream, Marker.Length);

        if (!marker.AsSpan().SequenceEqual(Marker))
            throw new TagLib.CorruptFileException("The FLAC stream marker was not found.");

        List<FlacMetadataBlock> blocks = [];
        List<string> fields = [];
        bool last;

        do
        {
            byte[] header = ReadExactly(stream, BlockHeaderSize);
            last = (header[0] & 0x80) != 0;
            byte type = (byte)(header[0] & 0x7F);
            int length = (header[1] << 16) | (header[2] << 8) | header[3];

            byte[] data = ReadExactly(stream, length);
            blocks.Add(new FlacMetadataBlock(type, data));

            if (type == VorbisCommentType)
                fields.AddRange(ParseVorbisFields(data));
        }
        while (!last);

        return new FlacLayoutInfo(stream.Position, blocks, fields);
    }

    private static long SkipLeadingId3v2(Stream stream)
    {
        long position = 0;

        while (true)
        {
            stream.Seek(position, SeekOrigin.Begin);

            byte[] header = new byte[Id3v2HeaderSize];

            if (stream.Read(header, 0, header.Length) < header.Length || !header.AsSpan(0, 3).SequenceEqual("ID3"u8))
                return position;

            int size = (header[6] << 21) | (header[7] << 14) | (header[8] << 7) | header[9];
            bool footer = (header[5] & Id3v2FooterFlag) != 0;

            position += Id3v2HeaderSize + size + (footer ? Id3v2HeaderSize : 0);
        }
    }

    private static List<string> ParseVorbisFields(byte[] data)
    {
        List<string> fields = [];
        int offset = 0;

        int vendorLength = ReadLength(data, ref offset);
        Advance(data, ref offset, vendorLength);

        int count = ReadLength(data, ref offset);

        for (int i = 0; i < count; i++)
        {
            int length = ReadLength(data, ref offset);

            if (length > data.Length - offset)
                throw new TagLib.CorruptFileException("A Vorbis comment field overruns its block.");

            fields.Add(Encoding.UTF8.GetString(data, offset, length));
            offset += length;
        }

        return fields;
    }

    private static int ReadLength(byte[] data, ref int offset)
    {
        if (data.Length - offset < sizeof(uint))
            throw new TagLib.CorruptFileException("The Vorbis comment block is truncated.");

        uint value = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset));
        offset += sizeof(uint);

        if (value > int.MaxValue)
            throw new TagLib.CorruptFileException("A Vorbis comment length is out of range.");

        return (int)value;
    }

    private static void Advance(byte[] data, ref int offset, int length)
    {
        if (length > data.Length - offset)
            throw new TagLib.CorruptFileException("The Vorbis comment vendor overruns its block.");

        offset += length;
    }

    private static byte[] ReadExactly(Stream stream, int length)
    {
        byte[] buffer = new byte[length];
        int total = 0;

        while (total < length)
        {
            int read = stream.Read(buffer, total, length - total);

            if (read == 0)
                throw new TagLib.CorruptFileException("The FLAC stream ends inside its metadata.");

            total += read;
        }

        return buffer;
    }
}