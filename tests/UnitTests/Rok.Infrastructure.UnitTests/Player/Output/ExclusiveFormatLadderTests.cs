using NAudio.Wave;
using Rok.Infrastructure.Player.Output;

namespace Rok.Infrastructure.UnitTests.Player.Output;

public class ExclusiveFormatLadderTests
{
    private static (int Container, int Valid, bool IsFloat)[] Describe(IReadOnlyList<WaveFormatExtensible> formats) =>
        [.. formats.Select(format => (format.BitsPerSample, (int)format.ValidBitsPerSample, ExclusiveFormatLadder.IsFloat(format)))];

    [Fact(DisplayName = "ladder_for_16_bit_is_16_then_24_in_32_then_float")]
    public void Candidates_ForSixteenBits_OrderedFromNativeToFloat()
    {
        // Act
        IReadOnlyList<WaveFormatExtensible> candidates = ExclusiveFormatLadder.Candidates(44100, 2, 16);

        // Assert
        Assert.Equal([(16, 16, false), (32, 24, false), (32, 32, true)], Describe(candidates));
        Assert.All(candidates, format =>
        {
            Assert.Equal(44100, format.SampleRate);
            Assert.Equal(2, format.Channels);
        });
    }

    [Fact(DisplayName = "ladder_for_24_bit_is_24_packed_then_24_in_32_then_float")]
    public void Candidates_ForTwentyFourBits_StartWithPacked()
    {
        // Act
        IReadOnlyList<WaveFormatExtensible> candidates = ExclusiveFormatLadder.Candidates(96000, 2, 24);

        // Assert
        Assert.Equal([(24, 24, false), (32, 24, false), (32, 32, true)], Describe(candidates));
        Assert.Equal(96000, candidates[0].SampleRate);
    }

    [Fact(DisplayName = "ladder_for_unknown_depth_behaves_as_24_bit")]
    public void Candidates_ForUnknownDepth_BehaveAsTwentyFourBits()
    {
        // Act
        IReadOnlyList<WaveFormatExtensible> candidates = ExclusiveFormatLadder.Candidates(44100, 2, 0);

        // Assert
        Assert.Equal(Describe(ExclusiveFormatLadder.Candidates(44100, 2, 24)), Describe(candidates));
    }

    [Fact(DisplayName = "ladder_above_24_bit_offers_float_only")]
    public void Candidates_AboveTwentyFourBits_OfferFloatOnly()
    {
        // Act
        IReadOnlyList<WaveFormatExtensible> candidates = ExclusiveFormatLadder.Candidates(192000, 2, 32);

        // Assert
        Assert.Equal([(32, 32, true)], Describe(candidates));
    }

    [Theory(DisplayName = "can_chain_rejects_different_rate_or_deeper_next_track")]
    [InlineData(48000, 2, 16)]
    [InlineData(44100, 1, 16)]
    [InlineData(44100, 2, 24)]
    [InlineData(44100, 2, 0)]
    public void CanChain_ReturnsFalse_ForIncompatibleNextTrack(int rate, int channels, int bits)
    {
        // Arrange
        WaveFormatExtensible negotiated = ExclusiveFormatLadder.Candidates(44100, 2, 16)[0];

        // Act
        bool canChain = ExclusiveFormatLadder.CanChain(negotiated, rate, channels, bits);

        // Assert
        Assert.False(canChain);
    }

    [Theory(DisplayName = "can_chain_accepts_shallower_next_track")]
    [InlineData(16)]
    [InlineData(24)]
    [InlineData(0)]
    public void CanChain_ReturnsTrue_ForShallowerOrEqualNextTrack(int bits)
    {
        // Arrange
        WaveFormatExtensible negotiated = ExclusiveFormatLadder.Candidates(96000, 2, 24)[1];

        // Act
        bool canChain = ExclusiveFormatLadder.CanChain(negotiated, 96000, 2, bits);

        // Assert
        Assert.True(canChain);
    }

    [Fact(DisplayName = "can_chain_accepts_any_depth_on_a_float_output")]
    public void CanChain_ReturnsTrue_OnFloatOutput()
    {
        // Arrange
        WaveFormatExtensible negotiated = ExclusiveFormatLadder.Candidates(44100, 2, 32)[0];

        // Act
        bool canChain = ExclusiveFormatLadder.CanChain(negotiated, 44100, 2, 32);

        // Assert
        Assert.True(canChain);
    }
}