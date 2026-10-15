using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Rok.Infrastructure.Tag.Formats;

namespace Rok.Infrastructure.UnitTests.Tag;

public sealed class Mp4LayoutTests
{
    private static readonly byte[] FtypBody = "M4A \0\0\0\0M4A isom"u8.ToArray();
    private static readonly byte[] MdatBody = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
    private static readonly byte[] SecondMdatBody = [11, 12, 13];

    [Fact(DisplayName = "mp4_layout_should_hash_every_root_box_except_moov_free_skip")]
    public void HashMediaBoxes_ShouldHashEveryRootBox_ExceptMoovFreeSkip()
    {
        // Arrange
        byte[] ftyp = Box("ftyp", FtypBody);
        byte[] mdat = Box("mdat", MdatBody);
        byte[] secondMdat = Box("mdat", SecondMdatBody);
        byte[] expected = SHA256.HashData([.. ftyp, .. mdat, .. secondMdat]);

        byte[] file = [.. ftyp, .. Box("free", [0, 0]), .. mdat, .. Box("moov", [7, 7, 7, 7]), .. Box("skip", [1]), .. secondMdat, .. Box("wide", [])];

        // Act
        string hash = Mp4Layout.HashMediaBoxes(new MemoryStream(file));

        // Assert
        Assert.Equal(Convert.ToHexString(expected), hash);
    }

    [Fact(DisplayName = "mp4_layout_should_not_change_the_media_hash_when_moov_moves_or_grows")]
    public void HashMediaBoxes_ShouldNotChange_WhenMoovMovesOrGrows()
    {
        // Arrange
        byte[] ftyp = Box("ftyp", FtypBody);
        byte[] mdat = Box("mdat", MdatBody);
        byte[] after = [.. ftyp, .. mdat, .. Box("moov", new byte[4])];
        byte[] before = [.. ftyp, .. Box("moov", new byte[64]), .. mdat];

        // Act
        string hashAfter = Mp4Layout.HashMediaBoxes(new MemoryStream(after));
        string hashBefore = Mp4Layout.HashMediaBoxes(new MemoryStream(before));

        // Assert
        Assert.Equal(hashAfter, hashBefore);
    }

    [Fact(DisplayName = "mp4_layout_should_change_the_media_hash_when_a_media_byte_changes")]
    public void HashMediaBoxes_ShouldChange_WhenAMediaByteChanges()
    {
        // Arrange
        byte[] file = [.. Box("ftyp", FtypBody), .. Box("mdat", MdatBody)];
        byte[] altered = [.. file];
        altered[^1] ^= 0xFF;

        // Act
        string original = Mp4Layout.HashMediaBoxes(new MemoryStream(file));
        string changed = Mp4Layout.HashMediaBoxes(new MemoryStream(altered));

        // Assert
        Assert.NotEqual(original, changed);
    }

    [Theory(DisplayName = "mp4_layout_should_detect_moof")]
    [InlineData(true)]
    [InlineData(false)]
    public void IsFragmented_ShouldDetectMoof(bool withMoof)
    {
        // Arrange
        byte[] file = withMoof
            ? [.. Box("ftyp", FtypBody), .. Box("moov", new byte[8]), .. Box("moof", new byte[8]), .. Box("mdat", MdatBody)]
            : [.. Box("ftyp", FtypBody), .. Box("moov", new byte[8]), .. Box("mdat", MdatBody)];

        // Act
        bool fragmented = Mp4Layout.IsFragmented(new MemoryStream(file));

        // Assert
        Assert.Equal(withMoof, fragmented);
    }

    [Fact(DisplayName = "mp4_layout_should_read_64_bit_box_sizes")]
    public void HashMediaBoxes_ShouldReadLargeBoxSizes()
    {
        // Arrange
        byte[] ftyp = Box("ftyp", FtypBody);
        byte[] largeMdat = LargeBox("mdat", MdatBody);
        byte[] expected = SHA256.HashData([.. ftyp, .. largeMdat]);
        byte[] file = [.. ftyp, .. largeMdat, .. Box("moov", new byte[4])];

        // Act
        string hash = Mp4Layout.HashMediaBoxes(new MemoryStream(file));

        // Assert
        Assert.Equal(Convert.ToHexString(expected), hash);
    }

    [Fact(DisplayName = "mp4_layout_should_read_a_last_box_that_extends_to_the_end_of_the_file")]
    public void HashMediaBoxes_ShouldReadBoxWithZeroSize()
    {
        // Arrange
        byte[] ftyp = Box("ftyp", FtypBody);
        byte[] toEnd = [0, 0, 0, 0, .. "mdat"u8, .. MdatBody];
        byte[] expected = SHA256.HashData([.. ftyp, .. toEnd]);

        // Act
        string hash = Mp4Layout.HashMediaBoxes(new MemoryStream([.. ftyp, .. toEnd]));

        // Assert
        Assert.Equal(Convert.ToHexString(expected), hash);
    }

    [Fact(DisplayName = "mp4_layout_should_throw_on_a_box_that_overruns_the_file")]
    public void HashMediaBoxes_ShouldThrow_OnBoxOverrunningTheFile()
    {
        // Arrange
        byte[] file = [.. Box("ftyp", FtypBody), 0, 0, 1, 0, .. "mdat"u8, 1, 2];

        // Act
        void Act() => Mp4Layout.HashMediaBoxes(new MemoryStream(file));

        // Assert
        Assert.Throws<TagLib.CorruptFileException>(Act);
    }

    [Fact(DisplayName = "mp4_layout_should_throw_corrupt_on_a_64_bit_size_that_would_overflow_the_bound_check")]
    public void HashMediaBoxes_ShouldThrowCorrupt_OnHugeLargeSize()
    {
        // Arrange
        byte[] hugeBox = [0, 0, 0, 1, .. "mdat"u8, .. new byte[] { 0x7F, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xF0 }];
        byte[] file = [.. Box("ftyp", FtypBody), .. hugeBox, .. new byte[8]];

        // Act
        void Act() => Mp4Layout.HashMediaBoxes(new MemoryStream(file));

        // Assert
        Assert.Throws<TagLib.CorruptFileException>(Act);
    }

    [Fact(DisplayName = "mp4_layout_should_throw_on_a_non_zero_tail_shorter_than_a_box_header")]
    public void HashMediaBoxes_ShouldThrow_OnNonZeroShortTail()
    {
        // Arrange
        byte[] file = [.. Box("ftyp", FtypBody), .. Box("mdat", MdatBody), 0, 0, 9];

        // Act
        void Act() => Mp4Layout.HashMediaBoxes(new MemoryStream(file));

        // Assert
        Assert.Throws<TagLib.CorruptFileException>(Act);
    }

    [Fact(DisplayName = "mp4_layout_should_tolerate_a_zero_tail_shorter_than_a_box_header")]
    public void HashMediaBoxes_ShouldTolerate_ZeroShortTail()
    {
        // Arrange
        byte[] file = [.. Box("ftyp", FtypBody), .. Box("mdat", MdatBody), 0, 0, 0];

        // Act
        string hash = Mp4Layout.HashMediaBoxes(new MemoryStream(file));

        // Assert
        Assert.NotEmpty(hash);
    }

    [Fact(DisplayName = "mp4_layout_should_throw_on_more_than_one_root_moov_box")]
    public void ReadIlstItems_ShouldThrow_OnTwoMoovBoxes()
    {
        // Arrange
        byte[] file = [.. Box("ftyp", FtypBody), .. Box("mdat", MdatBody), .. Box("moov", new byte[4]), .. Box("moov", new byte[4])];

        // Act
        void Act() => Mp4Layout.ReadIlstItems(new MemoryStream(file));

        // Assert
        Assert.Throws<TagLib.CorruptFileException>(Act);
    }

    [Fact(DisplayName = "mp4_layout_should_keep_relative_chunk_offsets_when_moov_moves")]
    public void HashRelativeChunkOffsets_ShouldKeepValue_WhenMoovMoves()
    {
        // Arrange
        byte[] ftyp = Box("ftyp", FtypBody);
        byte[] mdat = Box("mdat", MdatBody);
        int mdatAfter = ftyp.Length;
        byte[] after = [.. ftyp, .. mdat, .. Moov([mdatAfter + 8, mdatAfter + 12], includeIlst: false, [("©nam", "t")])];

        int moovBeforeSize = Moov([0, 0], includeIlst: false, [("©nam", "a longer title")]).Length;
        int mdatBefore = ftyp.Length + moovBeforeSize;
        byte[] before = [.. ftyp, .. Moov([mdatBefore + 8, mdatBefore + 12], includeIlst: false, [("©nam", "a longer title")]), .. mdat];

        // Act
        string hashAfter = Mp4Layout.HashRelativeChunkOffsets(new MemoryStream(after));
        string hashBefore = Mp4Layout.HashRelativeChunkOffsets(new MemoryStream(before));

        // Assert
        Assert.Equal(hashAfter, hashBefore);
    }

    [Fact(DisplayName = "mp4_layout_should_change_the_relative_chunk_offsets_hash_when_an_offset_is_wrong")]
    public void HashRelativeChunkOffsets_ShouldChange_WhenAnOffsetIsWrong()
    {
        // Arrange
        byte[] ftyp = Box("ftyp", FtypBody);
        byte[] mdat = Box("mdat", MdatBody);
        int mdatStart = ftyp.Length;
        byte[] correct = [.. ftyp, .. mdat, .. Moov([mdatStart + 8, mdatStart + 12], includeIlst: false, [])];
        byte[] wrong = [.. ftyp, .. mdat, .. Moov([mdatStart + 8, mdatStart + 13], includeIlst: false, [])];

        // Act
        string good = Mp4Layout.HashRelativeChunkOffsets(new MemoryStream(correct));
        string bad = Mp4Layout.HashRelativeChunkOffsets(new MemoryStream(wrong));

        // Assert
        Assert.NotEqual(good, bad);
    }

    [Fact(DisplayName = "mp4_layout_should_read_the_raw_children_of_ilst")]
    public void ReadIlstItems_ShouldReadRawChildren()
    {
        // Arrange
        byte[] file = [.. Box("ftyp", FtypBody), .. Box("mdat", MdatBody), .. Moov([0], includeIlst: true, [("©nam", "title"), ("©ART", "artist"), ("rokx", "value")])];

        // Act
        IReadOnlyList<Mp4IlstItem> items = Mp4Layout.ReadIlstItems(new MemoryStream(file));

        // Assert
        Assert.Equal(["©nam", "©ART", "rokx"], items.Select(i => i.Type));
        Assert.Equal(Item("©ART", "artist"), items[1].Data);
    }

    [Fact(DisplayName = "mp4_layout_should_return_no_ilst_items_without_a_moov_box")]
    public void ReadIlstItems_ShouldReturnNothing_WithoutMoov()
    {
        // Arrange
        byte[] file = [.. Box("ftyp", FtypBody), .. Box("mdat", MdatBody)];

        // Act
        IReadOnlyList<Mp4IlstItem> items = Mp4Layout.ReadIlstItems(new MemoryStream(file));

        // Assert
        Assert.Empty(items);
    }

    private static byte[] Box(string type, byte[] body)
    {
        byte[] box = new byte[8 + body.Length];

        BinaryPrimitives.WriteUInt32BigEndian(box, (uint)box.Length);
        Encoding.Latin1.GetBytes(type, box.AsSpan(4, 4));
        body.CopyTo(box, 8);

        return box;
    }

    private static byte[] LargeBox(string type, byte[] body)
    {
        byte[] box = new byte[16 + body.Length];

        BinaryPrimitives.WriteUInt32BigEndian(box, 1);
        Encoding.Latin1.GetBytes(type, box.AsSpan(4, 4));
        BinaryPrimitives.WriteUInt64BigEndian(box.AsSpan(8, 8), (ulong)box.Length);
        body.CopyTo(box, 16);

        return box;
    }

    private static byte[] Item(string type, string value)
    {
        byte[] data = [0, 0, 0, 1, 0, 0, 0, 0, .. Encoding.UTF8.GetBytes(value)];

        return Box(type, Box("data", data));
    }

    private static byte[] Moov(int[] chunkOffsets, bool includeIlst, (string Type, string Value)[] items)
    {
        byte[] stco = Box("stco", [0, 0, 0, 0, .. UInt32(chunkOffsets.Length), .. chunkOffsets.SelectMany(o => UInt32(o))]);
        byte[] trak = Box("trak", Box("mdia", Box("minf", Box("stbl", stco))));

        byte[] ilst = Box("ilst", [.. items.SelectMany(i => Item(i.Type, i.Value))]);
        byte[] meta = Box("meta", [0, 0, 0, 0, .. Box("hdlr", new byte[24]), .. ilst]);
        byte[] udta = Box("udta", meta);

        return Box("moov", [.. trak, .. (includeIlst || items.Length > 0 ? udta : [])]);
    }

    private static byte[] UInt32(int value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, (uint)value);

        return bytes;
    }
}