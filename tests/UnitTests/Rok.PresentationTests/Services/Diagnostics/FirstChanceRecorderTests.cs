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
}