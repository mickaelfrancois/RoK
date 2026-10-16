using System.Text;
using Rok.Infrastructure.Tag.Formats;

namespace Rok.Infrastructure.UnitTests.Tag;

public sealed class FlacLayoutTests
{
    [Fact(DisplayName = "flac_layout_should_locate_the_first_frame_after_a_leading_id3v2")]
    public void Read_ShouldLocateFirstFrame_AfterLeadingId3v2()
    {
        // Arrange
        byte[] id3 = [.. "ID3"u8, 3, 0, 0, 0, 0, 0, 5, 1, 2, 3, 4, 5];
        byte[] streamInfo = Block(0, new byte[34], last: false);
        byte[] comment = Block(FlacLayout.VorbisCommentType, VorbisComment("vendor", "TITLE=Song", "artist=Someone"), last: true);
        byte[] stream = [.. id3, .. "fLaC"u8, .. streamInfo, .. comment, 0xFF, 0xF8, 0x00];

        // Act
        FlacLayoutInfo layout = FlacLayout.Read(new MemoryStream(stream));

        // Assert
        Assert.Equal(id3.Length + 4 + streamInfo.Length + comment.Length, layout.FirstFrameOffset);
        Assert.Equal([(byte)0, FlacLayout.VorbisCommentType], layout.Blocks.Select(b => b.Type));
        Assert.Equal(["TITLE=Song", "artist=Someone"], layout.VorbisFields);
    }

    [Theory(DisplayName = "flac_tag_format_should_throw_when_the_audio_end_lies_before_the_first_frame")]
    [InlineData(100L, 5000L, 200L)]
    [InlineData(-1L, 150L, 200L)]
    public void ResolveAudioEnd_ShouldThrow_WhenEndBeforeFirstFrame(long invariantEnd, long fileLength, long firstFrameOffset)
    {
        // Arrange / Act
        void Act() => FlacTagFormat.ResolveAudioEnd(invariantEnd, fileLength, firstFrameOffset);

        // Assert
        Assert.Throws<TagLib.CorruptFileException>(Act);
    }

    [Theory(DisplayName = "flac_tag_format_should_resolve_the_audio_end")]
    [InlineData(900L, 1000L, 200L, 900L)]
    [InlineData(-1L, 1000L, 200L, 1000L)]
    [InlineData(200L, 1000L, 200L, 200L)]
    public void ResolveAudioEnd_ShouldReturnEnd(long invariantEnd, long fileLength, long firstFrameOffset, long expected)
    {
        // Arrange / Act
        long end = FlacTagFormat.ResolveAudioEnd(invariantEnd, fileLength, firstFrameOffset);

        // Assert
        Assert.Equal(expected, end);
    }

    [Fact(DisplayName = "flac_layout_should_skip_an_id3v2_footer")]
    public void Read_ShouldSkipId3v2Footer()
    {
        // Arrange
        byte[] id3 = [.. "ID3"u8, 4, 0, 0x10, 0, 0, 0, 2, 1, 2, .. "3DI"u8, 4, 0, 0x10, 0, 0, 0, 2];
        byte[] streamInfo = Block(0, new byte[34], last: true);
        byte[] stream = [.. id3, .. "fLaC"u8, .. streamInfo];

        // Act
        FlacLayoutInfo layout = FlacLayout.Read(new MemoryStream(stream));

        // Assert
        Assert.Equal(stream.Length, layout.FirstFrameOffset);
    }

    [Fact(DisplayName = "flac_layout_should_throw_without_flac_marker")]
    public void Read_ShouldThrow_WithoutFlacMarker()
    {
        // Arrange
        MemoryStream stream = new(new byte[64]);

        // Act
        Action act = () => FlacLayout.Read(stream);

        // Assert
        Assert.Throws<TagLib.CorruptFileException>(act);
    }

    [Fact(DisplayName = "flac_layout_should_throw_when_a_block_is_truncated")]
    public void Read_ShouldThrow_WhenBlockIsTruncated()
    {
        // Arrange
        byte[] stream = [.. "fLaC"u8, 0x80, 0, 0, 34, 1, 2, 3];

        // Act
        Action act = () => FlacLayout.Read(new MemoryStream(stream));

        // Assert
        Assert.Throws<TagLib.CorruptFileException>(act);
    }

    private static byte[] Block(byte type, byte[] data, bool last)
    {
        return [(byte)((last ? 0x80 : 0) | type), (byte)(data.Length >> 16), (byte)(data.Length >> 8), (byte)data.Length, .. data];
    }

    private static byte[] VorbisComment(string vendor, params string[] fields)
    {
        List<byte> bytes = [];

        AddString(bytes, vendor);
        bytes.AddRange(BitConverter.GetBytes((uint)fields.Length));

        foreach (string field in fields)
            AddString(bytes, field);

        return [.. bytes];
    }

    private static void AddString(List<byte> bytes, string value)
    {
        byte[] encoded = Encoding.UTF8.GetBytes(value);

        bytes.AddRange(BitConverter.GetBytes((uint)encoded.Length));
        bytes.AddRange(encoded);
    }
}