using Rok.Infrastructure.Player;

namespace Rok.Infrastructure.UnitTests.Player;

public class TransitionCueGateTests
{
    [Fact(DisplayName = "gate_does_not_raise_when_not_armed")]
    public void ShouldRaise_NotArmed_ReturnsFalse()
    {
        // Arrange
        TransitionCueGate gate = new();

        // Act
        bool raised = gate.ShouldRaise(1, 100);

        // Assert
        Assert.False(raised);
    }

    [Fact(DisplayName = "gate_does_not_raise_before_the_cue_position")]
    public void ShouldRaise_BeforeCue_ReturnsFalse()
    {
        // Arrange
        TransitionCueGate gate = new();
        gate.Arm(1, 50);

        // Act
        bool raised = gate.ShouldRaise(1, 49.9);

        // Assert
        Assert.False(raised);
    }

    [Fact(DisplayName = "gate_raises_once_when_the_cue_is_reached")]
    public void ShouldRaise_AtCue_RaisesOnce()
    {
        // Arrange
        TransitionCueGate gate = new();
        gate.Arm(1, 50);

        // Act
        bool first = gate.ShouldRaise(1, 50);
        bool second = gate.ShouldRaise(1, 50.25);

        // Assert
        Assert.True(first);
        Assert.False(second);
    }

    [Fact(DisplayName = "gate_ignores_a_cue_armed_for_another_track")]
    public void ShouldRaise_OtherTrack_ReturnsFalse()
    {
        // Arrange
        TransitionCueGate gate = new();
        gate.Arm(1, 50);

        // Act
        bool raised = gate.ShouldRaise(2, 60);

        // Assert
        Assert.False(raised);
    }

    [Fact(DisplayName = "gate_does_not_raise_after_clear")]
    public void ShouldRaise_AfterClear_ReturnsFalse()
    {
        // Arrange
        TransitionCueGate gate = new();
        gate.Arm(1, 50);
        gate.Clear();

        // Act
        bool raised = gate.ShouldRaise(1, 60);

        // Assert
        Assert.False(raised);
    }

    [Fact(DisplayName = "gate_raises_again_after_rearm")]
    public void ShouldRaise_AfterRearm_RaisesAgain()
    {
        // Arrange
        TransitionCueGate gate = new();
        gate.Arm(1, 50);
        gate.ShouldRaise(1, 50);
        gate.Rearm();

        // Act
        bool raised = gate.ShouldRaise(1, 55);

        // Assert
        Assert.True(raised);
    }

    [Fact(DisplayName = "gate_rearm_does_not_arm_a_cleared_cue")]
    public void ShouldRaise_RearmAfterClear_ReturnsFalse()
    {
        // Arrange
        TransitionCueGate gate = new();
        gate.Arm(1, 50);
        gate.Clear();
        gate.Rearm();

        // Act
        bool raised = gate.ShouldRaise(1, 60);

        // Assert
        Assert.False(raised);
    }

    [Fact(DisplayName = "gate_arming_again_replaces_the_previous_cue")]
    public void Arm_Twice_UsesTheLatestCue()
    {
        // Arrange
        TransitionCueGate gate = new();
        gate.Arm(1, 50);
        gate.ShouldRaise(1, 50);
        gate.Arm(2, 10);

        // Act
        bool oldTrack = gate.ShouldRaise(1, 60);
        bool newTrack = gate.ShouldRaise(2, 10);

        // Assert
        Assert.False(oldTrack);
        Assert.True(newTrack);
    }
}