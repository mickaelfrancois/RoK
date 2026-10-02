using System.Diagnostics;
using NAudio.Wave;
using Rok.Infrastructure.Player;
using Xunit.Abstractions;

namespace Rok.Infrastructure.UnitTests.Player;

public class TimeStretchSampleProviderTests(ITestOutputHelper output)
{
    private const int SampleRate = 48000;
    private const int Channels = 2;

    [Fact(DisplayName = "unsupported_format_is_rejected_without_allocating_and_start_throws")]
    public void Constructor_DoesNotAllocate_ForUnsupportedFormat()
    {
        // Arrange
        UnsupportedSource source = new();

        // Act
        TimeStretchSampleProvider sut = new(source);
        sut.SkipSource(TimeSpan.FromSeconds(1));

        // Assert
        Assert.False(TimeStretchSampleProvider.Supports(source.WaveFormat));
        Assert.Throws<NotSupportedException>(() => sut.Start(1.05, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1)));
        Assert.False(sut.IsActive);
    }

    [Fact(DisplayName = "pass_through_is_bit_exact_before_any_stretch")]
    public void Read_IsBitExact_BeforeAnyStretch()
    {
        // Arrange
        TimeStretchSampleProvider sut = new(Noise(SampleRate * 2));
        float[] expected = Render(Noise(SampleRate * 2), SampleRate * 2, 1000);

        // Act
        float[] actual = Render(sut, SampleRate * 2, 777);

        // Assert
        Assert.Equal(expected, actual);
        Assert.False(sut.IsActive);
    }

    [Fact(DisplayName = "rendering_is_bit_exact_once_the_return_is_complete")]
    public void Read_IsBitExact_OnceReturnIsComplete()
    {
        // Arrange
        const int sourceFrames = SampleRate * 8;
        TimeStretchSampleProvider sut = new(Noise(sourceFrames));
        sut.Start(1.05, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

        // Act
        float[] actual = RenderUntilEnd(sut, 1000);

        // Assert
        int frames = actual.Length / Channels;
        int contiguousFrom = (int)(2.1 * SampleRate);

        Assert.True(frames < sourceFrames);
        Assert.False(sut.IsActive);

        for (int frame = contiguousFrom; frame < frames; frame++)
        {
            int sourceFrame = sourceFrames - (frames - frame);

            Assert.Equal(NoiseValue(sourceFrame, 0), actual[frame * Channels]);
            Assert.Equal(NoiseValue(sourceFrame, 1), actual[(frame * Channels) + 1]);
        }
    }

    [Theory(DisplayName = "stretch_keeps_the_pitch_and_follows_the_ratio")]
    [InlineData(1.05)]
    [InlineData(0.95)]
    public void Read_KeepsPitch_AndFollowsRatio(double ratio)
    {
        // Arrange
        const int outputFrames = SampleRate * 30;
        SourceStub source = Sine(440, long.MaxValue);
        TimeStretchSampleProvider sut = new(source);
        sut.Start(ratio, TimeSpan.FromSeconds(1000), TimeSpan.FromSeconds(1));

        // Act
        float[] rendered = Render(sut, outputFrames, 1024);

        // Assert
        int skipped = SampleRate / 2;
        int crossings = 0;

        for (int frame = skipped + 1; frame < outputFrames; frame++)
        {
            if (rendered[(frame - 1) * Channels] < 0 && rendered[frame * Channels] >= 0)
                crossings++;
        }

        double frequency = crossings / ((outputFrames - skipped) / (double)SampleRate);
        double consumedPerEmitted = source.FramesRead / (double)outputFrames;

        Assert.InRange(frequency, 440 * 0.99, 440 * 1.01);
        Assert.InRange(consumedPerEmitted, ratio * 0.99, ratio * 1.01);
    }

    [Fact(DisplayName = "stretched_output_starts_on_the_first_source_frame")]
    public void Read_StartsOnFirstSourceFrame_WhenStretched()
    {
        // Arrange
        SourceStub source = Sine(50, long.MaxValue);
        TimeStretchSampleProvider sut = new(source);
        sut.Start(1.05, TimeSpan.FromSeconds(100), TimeSpan.FromSeconds(1));

        // Act
        float[] actual = Render(sut, SampleRate / 100, 240);

        // Assert
        for (int frame = 0; frame < actual.Length / Channels; frame++)
            Assert.Equal(SineValue(50, frame), actual[frame * Channels], 1e-6);
    }

    [Fact(DisplayName = "skip_source_discards_the_requested_frames_when_passing_through")]
    public void SkipSource_DiscardsFrames_WhenPassingThrough()
    {
        // Arrange
        TimeStretchSampleProvider sut = new(Noise(SampleRate * 2));
        sut.SkipSource(TimeSpan.FromSeconds(0.5));

        // Act
        float[] actual = Render(sut, 100, 100);

        // Assert
        for (int frame = 0; frame < 100; frame++)
            Assert.Equal(NoiseValue((SampleRate / 2) + frame, 0), actual[frame * Channels]);
    }

    [Fact(DisplayName = "skip_source_discards_the_requested_frames_when_stretched")]
    public void SkipSource_DiscardsFrames_WhenStretched()
    {
        // Arrange
        TimeStretchSampleProvider sut = new(Sine(50, long.MaxValue));
        sut.Start(1.05, TimeSpan.FromSeconds(100), TimeSpan.FromSeconds(1));
        sut.SkipSource(TimeSpan.FromSeconds(0.5));

        // Act
        float[] actual = Render(sut, 240, 240);

        // Assert
        for (int frame = 0; frame < 240; frame++)
            Assert.Equal(SineValue(50, (SampleRate / 2) + frame), actual[frame * Channels], 1e-6);
    }

    [Fact(DisplayName = "reset_restores_the_untouched_signal_at_once")]
    public void Reset_RestoresUntouchedSignal()
    {
        // Arrange
        SourceStub source = Noise(SampleRate * 6);
        TimeStretchSampleProvider sut = new(source);
        sut.Start(1.05, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(1));
        Render(sut, SampleRate, 1000);
        long consumed = source.FramesRead;

        // Act
        sut.Reset();
        float[] actual = Render(sut, 2000, 500);

        // Assert
        Assert.False(sut.IsActive);

        for (int frame = 0; frame < 2000; frame++)
            Assert.Equal(NoiseValue((int)consumed + frame, 0), actual[frame * Channels]);
    }

    [Fact(DisplayName = "return_to_original_tempo_splices_on_the_next_read")]
    public void ReturnToOriginalTempo_SplicesOnNextRead()
    {
        // Arrange
        const double ratio = 1.05;
        TimeStretchSampleProvider sut = new(Noise(SampleRate * 10));
        sut.Start(ratio, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(1));
        const int played = SampleRate * 2;
        Render(sut, played, 1000);

        // Act
        sut.ReturnToOriginalTempo();
        float[] actual = Render(sut, 4000, 1000);
        Render(sut, SampleRate, 1000);

        // Assert
        int spliceFrames = (int)(0.010 * SampleRate);
        int start = FindNoiseIndex(actual, spliceFrames);

        Assert.False(sut.IsActive);
        Assert.InRange(start - spliceFrames, (played * ratio) - (0.025 * SampleRate), (played * ratio) + (0.025 * SampleRate));

        for (int frame = spliceFrames; frame < 4000; frame++)
            Assert.Equal(NoiseValue(start + frame - spliceFrames, 0), actual[frame * Channels]);
    }

    [Theory(DisplayName = "stretched_read_does_not_allocate")]
    [InlineData(1.08)]
    [InlineData(1.03)]
    [InlineData(0.92)]
    public void Read_DoesNotAllocate_InSteadyState(double ratio)
    {
        // Arrange
        PlayLifecycle(ratio, null);
        long allocated = 0;

        // Act
        PlayLifecycle(ratio, delta => allocated += delta);

        // Assert
        output.WriteLine($"Bytes allocated by Read over the whole lifecycle at {ratio}: {allocated}");
        Assert.Equal(0, allocated);
    }

    [Theory(DisplayName = "unsupported_format_is_reported")]
    [InlineData(384000, 2, false)]
    [InlineData(48000, 17, false)]
    [InlineData(192000, 16, true)]
    [InlineData(44100, 2, true)]
    public void Supports_ReportsFormat(int sampleRate, int channels, bool expected)
    {
        // Arrange
        WaveFormat format = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);

        // Act
        bool actual = TimeStretchSampleProvider.Supports(format);

        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact(DisplayName = "end_of_file_during_stretch_flushes_and_ends")]
    public void Read_FlushesAndEnds_AtEndOfFile()
    {
        // Arrange
        const int sourceFrames = SampleRate * 3;
        const double ratio = 1.05;
        TimeStretchSampleProvider sut = new(Noise(sourceFrames));
        sut.Start(ratio, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(1));

        // Act
        float[] actual = RenderUntilEnd(sut, 1000);
        int after = sut.Read(new float[1000 * Channels]);

        // Assert
        double expectedFrames = sourceFrames / ratio;

        Assert.InRange(actual.Length / Channels, expectedFrames * 0.99, expectedFrames * 1.01);
        Assert.Equal(0, after);
        Assert.False(sut.IsActive);
    }

    [Theory(DisplayName = "read_sizes_not_aligned_on_the_blocks_do_not_throw")]
    [InlineData(1)]
    [InlineData(255)]
    [InlineData(2047)]
    [InlineData(4801)]
    public void Read_SizesNotAlignedOnBlocks_GiveTheSameRendering(int readFrames)
    {
        // Arrange
        const int frames = SampleRate * 2;
        TimeStretchSampleProvider reference = new(Noise(SampleRate * 6));
        reference.Start(1.05, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(1));
        TimeStretchSampleProvider sut = new(Noise(SampleRate * 6));
        sut.Start(1.05, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(1));
        float[] expected = Render(reference, frames, 1000);

        // Act
        float[] actual = Render(sut, frames, readFrames);

        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact(DisplayName = "two_stretched_pipelines_run_well_under_real_time")]
    public void Read_TwoStretchedProviders_RunWellUnderRealTime()
    {
        // Arrange
        const int seconds = 30;
        TimeStretchSampleProvider first = new(Sine(440, long.MaxValue));
        TimeStretchSampleProvider second = new(Sine(660, long.MaxValue));
        first.Start(1.06, TimeSpan.FromSeconds(1000), TimeSpan.FromSeconds(1));
        second.Start(0.94, TimeSpan.FromSeconds(1000), TimeSpan.FromSeconds(1));
        float[] buffer = new float[1024 * Channels];
        int blocks = seconds * SampleRate / 1024;

        // Act
        Stopwatch stopwatch = Stopwatch.StartNew();

        for (int block = 0; block < blocks; block++)
        {
            first.Read(buffer);
            second.Read(buffer);
        }

        stopwatch.Stop();

        // Assert
        double cpuPercent = stopwatch.Elapsed.TotalSeconds / (2 * seconds) * 100;

        output.WriteLine($"{seconds} s x 2 stretched stereo {SampleRate} Hz: {stopwatch.Elapsed.TotalMilliseconds:0} ms, {cpuPercent:0.00} % of one core per stream");
        Assert.True(stopwatch.Elapsed.TotalSeconds < 3);
    }

    private static void PlayLifecycle(double ratio, Action<long>? measured)
    {
        TimeStretchSampleProvider sut = new(Sine(220, SampleRate * 12));
        sut.SkipSource(TimeSpan.FromSeconds(0.1));
        sut.Start(ratio, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3));
        float[] buffer = new float[1000 * Channels];
        int blocks = SampleRate * 9 / 1000;

        for (int block = 0; block < blocks; block++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            int read = sut.Read(buffer);
            long after = GC.GetAllocatedBytesForCurrentThread();

            measured?.Invoke(after - before);

            if (read == 0)
                break;
        }
    }

    private static float[] Render(ISampleProvider provider, int frames, int readFrames)
    {
        float[] result = new float[frames * Channels];
        float[] buffer = new float[readFrames * Channels];
        int done = 0;

        while (done < frames)
        {
            int wanted = Math.Min(readFrames, frames - done);
            int read = provider.Read(buffer.AsSpan(0, wanted * Channels));

            if (read == 0)
                break;

            Array.Copy(buffer, 0, result, done * Channels, read);
            done += read / Channels;
        }

        return result.AsSpan(0, done * Channels).ToArray();
    }

    private static float[] RenderUntilEnd(ISampleProvider provider, int readFrames)
    {
        List<float> result = [];
        float[] buffer = new float[readFrames * Channels];
        int read;

        while ((read = provider.Read(buffer)) > 0)
            result.AddRange(buffer.AsSpan(0, read).ToArray());

        return [.. result];
    }

    /// <summary>Finds the source frame of the noise at <paramref name="outputFrame"/> by exhaustive lookup.</summary>
    private static int FindNoiseIndex(float[] rendered, int outputFrame)
    {
        float value = rendered[outputFrame * Channels];
        float other = rendered[(outputFrame * Channels) + 1];

        for (int frame = 0; frame < SampleRate * 12; frame++)
        {
            if (NoiseValue(frame, 0) == value && NoiseValue(frame, 1) == other)
                return frame;
        }

        throw new InvalidOperationException("The rendered frame does not come from the source.");
    }

    private static SourceStub Noise(long frames) => new(NoiseValue, frames);

    private static SourceStub Sine(double frequency, long frames) => new((frame, _) => SineValue(frequency, frame), frames);

    private static float SineValue(double frequency, long frame) => (float)(0.5 * Math.Sin(2 * Math.PI * frequency * frame / SampleRate));

    private static float NoiseValue(long frame, int channel)
    {
        unchecked
        {
            uint hash = (uint)(((ulong)frame * 2654435761UL) + ((ulong)(channel + 1) * 40503UL));

            hash ^= hash >> 15;
            hash *= 2246822519U;
            hash ^= hash >> 13;

            return ((hash >> 8) / 16777216f * 2 - 1) * 0.5f;
        }
    }

    private sealed class UnsupportedSource : ISampleProvider
    {
        public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, short.MaxValue);

        public int Read(Span<float> buffer) => 0;
    }

    private sealed class SourceStub(Func<long, int, float> sample, long totalFrames) : ISampleProvider
    {
        public long FramesRead { get; private set; }

        public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, Channels);

        public int Read(Span<float> buffer)
        {
            int frames = (int)Math.Min(buffer.Length / Channels, totalFrames - FramesRead);

            for (int frame = 0; frame < frames; frame++)
            {
                for (int channel = 0; channel < Channels; channel++)
                    buffer[(frame * Channels) + channel] = sample(FramesRead + frame, channel);
            }

            FramesRead += frames;

            return frames * Channels;
        }
    }
}