using Microsoft.Extensions.Time.Testing;
using Rok.Services.Diagnostics;

namespace Rok.PresentationTests.Services.Diagnostics;

public class FirstChanceRecorderTests
{
    [Fact(DisplayName = "record_keeps_the_throw_site_of_a_null_reference")]
    public void Record_KeepsTheThrowSiteOfANullReference()
    {
        // Arrange
        FirstChanceRecorder recorder = new(TimeProvider.System);

        // Act
        recorder.Record(new NullReferenceException());

        // Assert
        Assert.Contains(nameof(Record_KeepsTheThrowSiteOfANullReference), recorder.Snapshot());
    }

    [Fact(DisplayName = "record_ignores_other_exceptions")]
    public void Record_IgnoresOtherExceptions()
    {
        // Arrange
        FirstChanceRecorder recorder = new(TimeProvider.System);

        // Act
        recorder.Record(new InvalidOperationException());

        // Assert
        Assert.Null(recorder.Snapshot());
    }

    [Fact(DisplayName = "snapshot_stays_under_max_length")]
    public void Snapshot_StaysUnderMaxLength()
    {
        // Arrange
        FirstChanceRecorder recorder = new(TimeProvider.System);

        // Act
        recorder.Record(new ArgumentNullException());

        // Assert
        Assert.True(recorder.Snapshot()!.Length <= FirstChanceRecorder.MaxLength);
    }

    [Fact(DisplayName = "last_returns_null_when_nothing_was_recorded")]
    public void Last_NothingRecorded_ReturnsNull()
    {
        // Arrange
        FirstChanceRecorder recorder = new(TimeProvider.System);

        // Act
        FirstChanceSite? site = recorder.Last();

        // Assert
        Assert.Null(site);
    }

    [Fact(DisplayName = "last_returns_the_age_and_frames_of_the_recorded_throw")]
    public void Last_AfterRecord_ReturnsAgeAndFrames()
    {
        // Arrange
        FakeTimeProvider time = new();
        FirstChanceRecorder recorder = new(time);
        recorder.Record(new NullReferenceException());
        time.Advance(TimeSpan.FromMilliseconds(90));

        // Act
        FirstChanceSite? site = recorder.Last();

        // Assert
        Assert.NotNull(site);
        Assert.Equal(90, site.AgeMs);
        Assert.Contains(nameof(Last_AfterRecord_ReturnsAgeAndFrames), site.Frames);
    }
}