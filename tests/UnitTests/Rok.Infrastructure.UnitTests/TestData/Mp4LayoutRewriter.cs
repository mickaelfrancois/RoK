using System.Buffers.Binary;
using System.Text;

namespace Rok.Infrastructure.UnitTests.TestData;

/// <summary>
/// Moves the <c>moov</c> box of an MP4 file before or after <c>mdat</c> and fixes every chunk offset
/// (<c>stco</c> / <c>co64</c>), without relying on production code or TagLib.
/// </summary>
internal static class Mp4LayoutRewriter
{
    private static readonly string[] SampleTablePath = ["trak", "mdia", "minf", "stbl"];

    private readonly record struct RootBox(string Type, int Start, int Size);

    /// <summary>Rewrites the file in place so that <c>moov</c> sits in the requested position.</summary>
    public static void Rewrite(string path, EMp4Layout layout)
    {
        byte[] bytes = File.ReadAllBytes(path);
        List<RootBox> boxes = ReadRootBoxes(bytes);

        RootBox moov = boxes.Single(b => b.Type == "moov");
        List<RootBox> others = boxes.Where(b => b.Type != "moov").ToList();

        List<RootBox> ordered = [.. others];

        if (layout == EMp4Layout.MoovBeforeMdat)
        {
            int firstMdat = ordered.FindIndex(b => b.Type == "mdat");
            ordered.Insert(firstMdat < 0 ? ordered.Count : firstMdat, moov);
        }
        else
        {
            ordered.Add(moov);
        }

        Dictionary<int, int> newStarts = [];
        int position = 0;

        foreach (RootBox box in ordered)
        {
            newStarts[box.Start] = position;
            position += box.Size;
        }

        byte[] patchedMoov = bytes.AsSpan(moov.Start, moov.Size).ToArray();
        PatchContainer(patchedMoov, 8, patchedMoov.Length, 0, others, newStarts);

        using MemoryStream output = new(position);

        foreach (RootBox box in ordered)
        {
            if (box.Type == "moov")
                output.Write(patchedMoov);
            else
                output.Write(bytes, box.Start, box.Size);
        }

        File.WriteAllBytes(path, output.ToArray());
    }

    /// <summary>Returns the order of the root boxes, for example <c>ftyp,moov,mdat</c>.</summary>
    public static string[] GetRootBoxTypes(string path)
    {
        return [.. ReadRootBoxes(File.ReadAllBytes(path)).Select(b => b.Type)];
    }

    /// <summary>Returns the layout of the file based on the first <c>moov</c> and the first <c>mdat</c>.</summary>
    public static EMp4Layout GetLayout(string path)
    {
        string[] types = GetRootBoxTypes(path);

        return Array.IndexOf(types, "moov") < Array.IndexOf(types, "mdat") ? EMp4Layout.MoovBeforeMdat : EMp4Layout.MoovAfterMdat;
    }

    private static List<RootBox> ReadRootBoxes(byte[] bytes)
    {
        List<RootBox> boxes = [];
        int position = 0;

        while (position + 8 <= bytes.Length)
        {
            long size = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(position, 4));
            string type = Encoding.Latin1.GetString(bytes, position + 4, 4);

            if (size == 1)
                size = (long)BinaryPrimitives.ReadUInt64BigEndian(bytes.AsSpan(position + 8, 8));
            else if (size == 0)
                size = bytes.Length - position;

            if (size < 8 || position + size > bytes.Length)
                throw new InvalidDataException($"Invalid box '{type}' at {position}.");

            boxes.Add(new RootBox(type, position, (int)size));
            position += (int)size;
        }

        return boxes;
    }

    private static void PatchContainer(byte[] data, int start, int end, int depth, List<RootBox> oldBoxes, Dictionary<int, int> newStarts)
    {
        int position = start;

        while (position + 8 <= end)
        {
            int size = checked((int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(position, 4)));
            string type = Encoding.Latin1.GetString(data, position + 4, 4);

            if (size < 8 || position + size > end)
                throw new InvalidDataException($"Invalid child box '{type}' in moov at {position}.");

            if (depth < SampleTablePath.Length)
            {
                if (type == SampleTablePath[depth])
                    PatchContainer(data, position + 8, position + size, depth + 1, oldBoxes, newStarts);
            }
            else if (type is "stco" or "co64")
            {
                PatchOffsetTable(data, position, size, type == "co64", oldBoxes, newStarts);
            }

            position += size;
        }
    }

    private static void PatchOffsetTable(byte[] data, int boxStart, int boxSize, bool is64Bit, List<RootBox> oldBoxes, Dictionary<int, int> newStarts)
    {
        int entrySize = is64Bit ? 8 : 4;
        int count = checked((int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(boxStart + 12, 4)));
        int first = boxStart + 16;

        if (first + (count * entrySize) > boxStart + boxSize)
            throw new InvalidDataException("Chunk offset table overruns its box.");

        for (int i = 0; i < count; i++)
        {
            Span<byte> entry = data.AsSpan(first + (i * entrySize), entrySize);
            long old = is64Bit ? (long)BinaryPrimitives.ReadUInt64BigEndian(entry) : BinaryPrimitives.ReadUInt32BigEndian(entry);

            RootBox container = oldBoxes.FirstOrDefault(b => old >= b.Start && old < b.Start + b.Size);

            if (container.Type is null)
                throw new InvalidDataException($"Chunk offset {old} points outside any root box.");

            long moved = old - container.Start + newStarts[container.Start];

            if (is64Bit)
                BinaryPrimitives.WriteUInt64BigEndian(entry, (ulong)moved);
            else
                BinaryPrimitives.WriteUInt32BigEndian(entry, checked((uint)moved));
        }
    }
}