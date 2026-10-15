using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Rok.Infrastructure.Tag.Formats;

/// <summary>
/// A raw child of the <c>ilst</c> box.
/// </summary>
/// <param name="Type">Four-character atom type; <c>©nam</c> is the title.</param>
/// <param name="Data">The whole box, header included.</param>
internal sealed record Mp4IlstItem(string Type, byte[] Data);

/// <summary>
/// Minimal MP4 box parser that proves the media boxes and the chunk offsets are intact, without relying on TagLib's invariant positions.
/// </summary>
internal static class Mp4Layout
{
    /// <summary>Atom type of the title (<c>©nam</c>).</summary>
    public const string TitleAtomType = "©nam";

    private const long BoxHeaderSize = 8;
    private const long LargeBoxHeaderSize = 16;
    private const int FullBoxExtraSize = 4;

    private static readonly string[] SampleTablePath = ["trak", "mdia", "minf", "stbl"];
    private static readonly string[] IlstPath = ["udta", "meta", "ilst"];
    private static readonly HashSet<string> NonMediaRootBoxes = ["moov", "free", "skip", "wide"];

    private readonly record struct Box(string Type, long Start, long HeaderSize, long Size)
    {
        public long ContentStart => Start + HeaderSize;

        public long End => Start + Size;
    }

    /// <summary>
    /// Tells whether the file is a fragmented MP4 (a <c>moof</c> box at the root).
    /// </summary>
    /// <param name="path">File to inspect.</param>
    /// <returns><c>true</c> when the file is fragmented.</returns>
    public static bool IsFragmented(string path)
    {
        using FileStream stream = OpenRead(path);

        return IsFragmented(stream);
    }

    /// <inheritdoc cref="IsFragmented(string)"/>
    /// <param name="stream">Seekable stream.</param>
    public static bool IsFragmented(Stream stream)
    {
        return EnumerateRootBoxes(stream).Any(b => b.Type == "moof");
    }

    /// <summary>
    /// Hashes every root box except <c>moov</c>, <c>free</c>, <c>skip</c> and <c>wide</c>, concatenated in file order.
    /// </summary>
    /// <param name="path">File to hash.</param>
    /// <returns>The SHA-256 as an uppercase hexadecimal string.</returns>
    public static string HashMediaBoxes(string path)
    {
        using FileStream stream = OpenRead(path);

        return HashMediaBoxes(stream);
    }

    /// <inheritdoc cref="HashMediaBoxes(string)"/>
    /// <param name="stream">Seekable stream.</param>
    public static string HashMediaBoxes(Stream stream)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = new byte[81920];

        foreach (Box box in EnumerateRootBoxes(stream).ToList())
        {
            if (NonMediaRootBoxes.Contains(box.Type))
                continue;

            stream.Seek(box.Start, SeekOrigin.Begin);

            long remaining = box.Size;

            while (remaining > 0)
            {
                int read = stream.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));

                if (read == 0)
                    throw new TagLib.CorruptFileException("The MP4 stream ends inside a box.");

                hash.AppendData(buffer, 0, read);
                remaining -= read;
            }
        }

        return Convert.ToHexString(hash.GetHashAndReset());
    }

    /// <summary>
    /// Hashes the chunk offsets (<c>stco</c> / <c>co64</c>) of every track, relative to the start of the first <c>mdat</c>.
    /// The result does not change when <c>moov</c> moves, only when an offset stops pointing at the same media bytes.
    /// </summary>
    /// <param name="path">File to read.</param>
    /// <returns>The SHA-256 as an uppercase hexadecimal string.</returns>
    public static string HashRelativeChunkOffsets(string path)
    {
        using FileStream stream = OpenRead(path);

        return HashRelativeChunkOffsets(stream);
    }

    /// <inheritdoc cref="HashRelativeChunkOffsets(string)"/>
    /// <param name="stream">Seekable stream.</param>
    public static string HashRelativeChunkOffsets(Stream stream)
    {
        List<Box> roots = EnumerateRootBoxes(stream).ToList();
        Box firstMdat = roots.FirstOrDefault(b => b.Type == "mdat");
        long origin = firstMdat.Type is null ? 0 : firstMdat.Start;

        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[]? moov = ReadMoov(stream, roots, out int moovContent);

        if (moov is not null)
        {
            foreach ((int start, int end) in FindAll(moov, moovContent, moov.Length, SampleTablePath))
            {
                foreach (Box table in EnumerateChildren(moov, start, end).Where(b => b.Type is "stco" or "co64"))
                    AppendOffsets(hash, moov, table, origin);
            }
        }

        return Convert.ToHexString(hash.GetHashAndReset());
    }

    /// <summary>
    /// Reads the raw children of <c>moov/udta/meta/ilst</c>.
    /// </summary>
    /// <param name="path">File to read.</param>
    /// <returns>The items in file order, empty when the file has no <c>ilst</c> box.</returns>
    public static IReadOnlyList<Mp4IlstItem> ReadIlstItems(string path)
    {
        using FileStream stream = OpenRead(path);

        return ReadIlstItems(stream);
    }

    /// <inheritdoc cref="ReadIlstItems(string)"/>
    /// <param name="stream">Seekable stream.</param>
    public static IReadOnlyList<Mp4IlstItem> ReadIlstItems(Stream stream)
    {
        byte[]? moov = ReadMoov(stream, EnumerateRootBoxes(stream).ToList(), out int moovContent);

        if (moov is null)
            return [];

        List<Mp4IlstItem> items = [];

        foreach ((int start, int end) in FindAll(moov, moovContent, moov.Length, IlstPath))
        {
            foreach (Box item in EnumerateChildren(moov, start, end))
                items.Add(new Mp4IlstItem(item.Type, moov.AsSpan((int)item.Start, (int)item.Size).ToArray()));
        }

        return items;
    }

    private static FileStream OpenRead(string path)
    {
        return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    private static IEnumerable<Box> EnumerateRootBoxes(Stream stream)
    {
        long position = 0;
        long length = stream.Length;
        int moovCount = 0;
        byte[] header = new byte[LargeBoxHeaderSize];

        while (length - position >= BoxHeaderSize)
        {
            stream.Seek(position, SeekOrigin.Begin);
            stream.ReadExactly(header, 0, (int)BoxHeaderSize);

            long size = BinaryPrimitives.ReadUInt32BigEndian(header);
            string type = Encoding.Latin1.GetString(header, 4, 4);
            long headerSize = BoxHeaderSize;

            if (size == 1)
            {
                if (length - position < LargeBoxHeaderSize)
                    throw new TagLib.CorruptFileException($"The MP4 box '{type}' is truncated.");

                stream.ReadExactly(header, (int)BoxHeaderSize, (int)BoxHeaderSize);
                ulong large = BinaryPrimitives.ReadUInt64BigEndian(header.AsSpan((int)BoxHeaderSize));

                if (large > long.MaxValue)
                    throw new TagLib.CorruptFileException($"The MP4 box '{type}' has an invalid size.");

                size = (long)large;
                headerSize = LargeBoxHeaderSize;
            }
            else if (size == 0)
            {
                size = length - position;
            }

            if (size < headerSize || size > length - position)
                throw new TagLib.CorruptFileException($"The MP4 box '{type}' has an invalid size.");

            if (type == "moov" && ++moovCount > 1)
                throw new TagLib.CorruptFileException("The MP4 file has more than one moov box.");

            yield return new Box(type, position, headerSize, size);

            position += size;
        }

        ThrowOnNonZeroTail(stream, position, length);
    }

    private static void ThrowOnNonZeroTail(Stream stream, long position, long length)
    {
        if (position >= length)
            return;

        byte[] tail = new byte[length - position];
        stream.Seek(position, SeekOrigin.Begin);
        stream.ReadExactly(tail);

        if (tail.Any(b => b != 0))
            throw new TagLib.CorruptFileException("The MP4 file ends with unparsed bytes.");
    }

    private static byte[]? ReadMoov(Stream stream, List<Box> roots, out int contentStart)
    {
        contentStart = 0;

        foreach (Box box in roots)
        {
            if (box.Type != "moov")
                continue;

            if (box.Size > int.MaxValue)
                throw new TagLib.CorruptFileException("The MP4 moov box is too large.");

            byte[] data = new byte[box.Size];
            stream.Seek(box.Start, SeekOrigin.Begin);
            stream.ReadExactly(data);

            contentStart = (int)box.HeaderSize;

            return data;
        }

        return null;
    }

    private static IEnumerable<Box> EnumerateChildren(byte[] data, int start, int end)
    {
        long position = start;

        while (end - position >= BoxHeaderSize)
        {
            long size = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan((int)position, 4));
            string type = Encoding.Latin1.GetString(data, (int)position + 4, 4);
            long headerSize = BoxHeaderSize;

            if (size == 1)
            {
                if (end - position < LargeBoxHeaderSize)
                    throw new TagLib.CorruptFileException($"The MP4 box '{type}' is truncated.");

                ulong large = BinaryPrimitives.ReadUInt64BigEndian(data.AsSpan((int)position + (int)BoxHeaderSize, 8));

                if (large > long.MaxValue)
                    throw new TagLib.CorruptFileException($"The MP4 box '{type}' has an invalid size.");

                size = (long)large;
                headerSize = LargeBoxHeaderSize;
            }
            else if (size == 0)
            {
                size = end - position;
            }

            if (size < headerSize || size > end - position)
                throw new TagLib.CorruptFileException($"The MP4 box '{type}' has an invalid size.");

            yield return new Box(type, position, headerSize, size);

            position += size;
        }
    }

    private static IEnumerable<(int Start, int End)> FindAll(byte[] data, int start, int end, string[] path, int depth = 0)
    {
        if (depth == path.Length)
        {
            yield return (start, end);

            yield break;
        }

        foreach (Box child in EnumerateChildren(data, start, end).Where(b => b.Type == path[depth]))
        {
            int contentStart = (int)child.ContentStart;

            if (path[depth] == "meta")
                contentStart += FullBoxExtraSize;

            if (contentStart > child.End)
                throw new TagLib.CorruptFileException("The MP4 meta box is truncated.");

            foreach ((int Start, int End) found in FindAll(data, contentStart, (int)child.End, path, depth + 1))
                yield return found;
        }
    }

    private static void AppendOffsets(IncrementalHash hash, byte[] data, Box table, long origin)
    {
        bool is64Bit = table.Type == "co64";
        int entrySize = is64Bit ? sizeof(ulong) : sizeof(uint);
        int countPosition = (int)table.ContentStart + FullBoxExtraSize;

        if (table.End - countPosition < sizeof(uint))
            throw new TagLib.CorruptFileException("A chunk offset table is truncated.");

        uint count = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(countPosition, sizeof(uint)));
        int first = countPosition + sizeof(uint);

        if (count > (table.End - first) / entrySize)
            throw new TagLib.CorruptFileException("A chunk offset table overruns its box.");

        Span<byte> buffer = stackalloc byte[sizeof(long)];

        BinaryPrimitives.WriteInt64LittleEndian(buffer, count);
        hash.AppendData(buffer);

        for (int i = 0; i < count; i++)
        {
            ReadOnlySpan<byte> entry = data.AsSpan(first + (i * entrySize), entrySize);
            long offset = is64Bit ? (long)BinaryPrimitives.ReadUInt64BigEndian(entry) : BinaryPrimitives.ReadUInt32BigEndian(entry);

            BinaryPrimitives.WriteInt64LittleEndian(buffer, offset - origin);
            hash.AppendData(buffer);
        }
    }
}