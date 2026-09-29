using NAudio.Wave;
using Rok.Infrastructure.Player;

namespace Rok.Infrastructure.UnitTests.Player;

public class GaplessChainSampleProviderTests
{
    [Fact(DisplayName = "read_fills_the_buffer_across_the_boundary_without_inserting_zeros")]
    public void Read_FillsBufferAcrossBoundary_WithoutInsertingZeros()
    {
        // Arrange
        FiniteSampleProvider first = new(sampleCount: 6, value: 1f);
        FiniteSampleProvider second = new(sampleCount: 100, value: 2f);
        GaplessChainSampleProvider chain = new(first);
        chain.TryQueueNext(second, out _);

        float[] buffer = new float[10];

        // Act
        int read = chain.Read(buffer);

        // Assert
        Assert.Equal(10, read);
        Assert.All(buffer[..6], sample => Assert.Equal(1f, sample));
        Assert.All(buffer[6..], sample => Assert.Equal(2f, sample));
        Assert.DoesNotContain(0f, buffer);
    }

    [Theory(DisplayName = "read_switches_exactly_after_the_last_sample_of_the_current_source")]
    [InlineData(12, 4)]
    [InlineData(13, 4)]
    [InlineData(7, 16)]
    [InlineData(1, 3)]
    public void Read_SwitchesExactlyAfterLastSampleOfCurrentSource(int firstSampleCount, int bufferSize)
    {
        // Arrange
        FiniteSampleProvider first = new(firstSampleCount, value: 1f);
        FiniteSampleProvider second = new(sampleCount: 100, value: 2f);
        GaplessChainSampleProvider chain = new(first);
        chain.TryQueueNext(second, out _);

        List<float> output = [];
        float[] buffer = new float[bufferSize];

        // Act
        while (output.Count < firstSampleCount + 10)
        {
            int read = chain.Read(buffer);
            output.AddRange(buffer[..read]);
        }

        // Assert
        Assert.Equal(firstSampleCount, output.IndexOf(2f));
        Assert.All(output[..firstSampleCount], sample => Assert.Equal(1f, sample));
        Assert.DoesNotContain(0f, output);
    }

    [Fact(DisplayName = "read_raises_source_switched_once_at_the_boundary")]
    public void Read_RaisesSourceSwitchedOnce_AtBoundary()
    {
        // Arrange
        FiniteSampleProvider first = new(sampleCount: 5, value: 1f);
        FiniteSampleProvider second = new(sampleCount: 100, value: 2f);
        GaplessChainSampleProvider chain = new(first);
        chain.TryQueueNext(second, out _);

        List<SourceSwitchedEventArgs> events = [];
        chain.SourceSwitched += (_, e) => events.Add(e);

        float[] buffer = new float[4];

        // Act
        for (int i = 0; i < 5; i++)
            chain.Read(buffer);

        // Assert
        SourceSwitchedEventArgs switched = Assert.Single(events);
        Assert.Same(first, switched.Previous);
        Assert.Same(second, switched.Current);
        Assert.Same(second, chain.Current);
    }

    [Fact(DisplayName = "read_does_not_switch_on_a_short_non_zero_read")]
    public void Read_DoesNotSwitch_OnShortNonZeroRead()
    {
        // Arrange
        FiniteSampleProvider first = new(sampleCount: 9, value: 1f, maxPerRead: 2);
        FiniteSampleProvider second = new(sampleCount: 100, value: 2f);
        GaplessChainSampleProvider chain = new(first);
        chain.TryQueueNext(second, out _);

        float[] buffer = new float[12];

        // Act
        int read = chain.Read(buffer);

        // Assert
        Assert.Equal(12, read);
        Assert.All(buffer[..9], sample => Assert.Equal(1f, sample));
        Assert.All(buffer[9..], sample => Assert.Equal(2f, sample));
    }

    [Fact(DisplayName = "read_returns_partial_count_then_zero_when_no_next_source")]
    public void Read_ReturnsPartialCountThenZero_WhenNoNextSource()
    {
        // Arrange
        FiniteSampleProvider first = new(sampleCount: 6, value: 1f);
        GaplessChainSampleProvider chain = new(first);

        float[] buffer = new float[10];

        // Act
        int firstRead = chain.Read(buffer);
        int secondRead = chain.Read(buffer);

        // Assert
        Assert.Equal(6, firstRead);
        Assert.Equal(0, secondRead);
    }

    [Fact(DisplayName = "try_queue_next_rejects_a_different_sample_rate")]
    public void TryQueueNext_RejectsDifferentSampleRate()
    {
        // Arrange
        FiniteSampleProvider first = new(sampleCount: 4, value: 1f, sampleRate: 44100);
        FiniteSampleProvider second = new(sampleCount: 4, value: 2f, sampleRate: 48000);
        GaplessChainSampleProvider chain = new(first);

        // Act
        bool queued = chain.TryQueueNext(second, out ISampleProvider? replaced);

        // Assert
        Assert.False(queued);
        Assert.Null(replaced);
        Assert.Null(chain.ClearNext());
    }

    [Fact(DisplayName = "try_queue_next_rejects_a_different_channel_count")]
    public void TryQueueNext_RejectsDifferentChannelCount()
    {
        // Arrange
        FiniteSampleProvider first = new(sampleCount: 4, value: 1f, channels: 2);
        FiniteSampleProvider second = new(sampleCount: 4, value: 2f, channels: 1);
        GaplessChainSampleProvider chain = new(first);

        // Act
        bool queued = chain.TryQueueNext(second, out _);

        // Assert
        Assert.False(queued);
        Assert.Null(chain.ClearNext());
    }

    [Fact(DisplayName = "clear_next_before_the_end_prevents_the_switch")]
    public void ClearNext_BeforeEnd_PreventsSwitch()
    {
        // Arrange
        FiniteSampleProvider first = new(sampleCount: 6, value: 1f);
        FiniteSampleProvider second = new(sampleCount: 100, value: 2f);
        GaplessChainSampleProvider chain = new(first);
        chain.TryQueueNext(second, out _);

        bool switched = false;
        chain.SourceSwitched += (_, _) => switched = true;

        float[] buffer = new float[10];

        // Act
        ISampleProvider? removed = chain.ClearNext();
        int firstRead = chain.Read(buffer);
        int secondRead = chain.Read(buffer);

        // Assert
        Assert.Same(second, removed);
        Assert.Equal(6, firstRead);
        Assert.Equal(0, secondRead);
        Assert.False(switched);
    }

    [Fact(DisplayName = "clear_next_returns_null_when_nothing_is_queued")]
    public void ClearNext_ReturnsNull_WhenNothingIsQueued()
    {
        // Arrange
        GaplessChainSampleProvider chain = new(new FiniteSampleProvider(sampleCount: 4, value: 1f));

        // Act
        ISampleProvider? removed = chain.ClearNext();

        // Assert
        Assert.Null(removed);
    }

    [Fact(DisplayName = "try_queue_next_replaces_a_previously_queued_source")]
    public void TryQueueNext_ReplacesPreviouslyQueuedSource()
    {
        // Arrange
        FiniteSampleProvider first = new(sampleCount: 2, value: 1f);
        FiniteSampleProvider stale = new(sampleCount: 100, value: 2f);
        FiniteSampleProvider fresh = new(sampleCount: 100, value: 3f);
        GaplessChainSampleProvider chain = new(first);
        chain.TryQueueNext(stale, out _);

        float[] buffer = new float[4];

        // Act
        bool queued = chain.TryQueueNext(fresh, out ISampleProvider? replaced);
        chain.Read(buffer);

        // Assert
        Assert.True(queued);
        Assert.Same(stale, replaced);
        Assert.Equal([1f, 1f, 3f, 3f], buffer);
    }

    private sealed class FiniteSampleProvider(int sampleCount, float value, int maxPerRead = int.MaxValue, int sampleRate = 44100, int channels = 2) : ISampleProvider
    {
        private int _remaining = sampleCount;

        public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);

        public int Read(Span<float> buffer)
        {
            int count = Math.Min(Math.Min(_remaining, buffer.Length), maxPerRead);
            buffer[..count].Fill(value);
            _remaining -= count;

            return count;
        }
    }
}