using Rok.Application.Player.Output;

namespace Rok.ApplicationTests.Player.Output;

public class AudioDevicePolicyTests
{
    private const string Speakers = "speakers";
    private const string Dac = "dac";
    private const string Headset = "headset";

    private static readonly AudioOutputTarget OnDac = new(Dac, EAudioOutputMode.Shared);
    private static readonly AudioOutputTarget OnWindowsDefault = AudioOutputTarget.Default;

    private static AudioDeviceSnapshot Snapshot(string? defaultId, params string[] active) =>
        new([.. active.Select(id => new AudioDeviceDto(id, id))], defaultId);

    private static AudioOutputState OpenOn(string deviceId, bool isOnPreferred) =>
        new(true, deviceId, isOnPreferred, EAudioOutputMode.Shared, null, true);

    [Fact(DisplayName = "resolve_device_returns_preferred_when_active")]
    public void ResolveDeviceId_ReturnsPreferred_WhenActive()
    {
        // Act
        string? deviceId = AudioDevicePolicy.ResolveDeviceId(Dac, Snapshot(Speakers, Speakers, Dac));

        // Assert
        Assert.Equal(Dac, deviceId);
    }

    [Fact(DisplayName = "resolve_device_falls_back_to_windows_default_when_preferred_missing")]
    public void ResolveDeviceId_FallsBackToWindowsDefault_WhenPreferredMissing()
    {
        // Act
        string? deviceId = AudioDevicePolicy.ResolveDeviceId(Dac, Snapshot(Speakers, Speakers));

        // Assert
        Assert.Equal(Speakers, deviceId);
    }

    [Fact(DisplayName = "resolve_device_uses_windows_default_without_preference")]
    public void ResolveDeviceId_UsesWindowsDefault_WithoutPreference()
    {
        // Act
        string? deviceId = AudioDevicePolicy.ResolveDeviceId(string.Empty, Snapshot(Headset, Speakers, Headset));

        // Assert
        Assert.Equal(Headset, deviceId);
    }

    [Fact(DisplayName = "resolve_device_returns_null_without_any_device")]
    public void ResolveDeviceId_ReturnsNull_WithoutAnyDevice()
    {
        // Act
        string? deviceId = AudioDevicePolicy.ResolveDeviceId(Dac, AudioDeviceSnapshot.Empty);

        // Assert
        Assert.Null(deviceId);
    }

    [Fact(DisplayName = "output_lost_on_explicit_preferred_decides_pause")]
    public void DecideOnOutputLost_Pauses_WhenExplicitPreferredDisappears()
    {
        // Act
        EAudioDeviceAction action = AudioDevicePolicy.DecideOnOutputLost(OnDac, new OutputLostEventArgs(true, true), Snapshot(Speakers, Speakers));

        // Assert
        Assert.Equal(EAudioDeviceAction.Pause, action);
    }

    [Fact(DisplayName = "output_lost_on_preferred_still_active_decides_reopen")]
    public void DecideOnOutputLost_Reopens_WhenPreferredIsStillActive()
    {
        // Act
        EAudioDeviceAction action = AudioDevicePolicy.DecideOnOutputLost(OnDac, new OutputLostEventArgs(true, true), Snapshot(Speakers, Speakers, Dac));

        // Assert
        Assert.Equal(EAudioDeviceAction.Reopen, action);
    }

    [Fact(DisplayName = "output_lost_in_windows_default_mode_decides_reopen")]
    public void DecideOnOutputLost_Reopens_InWindowsDefaultMode()
    {
        // Act
        EAudioDeviceAction action = AudioDevicePolicy.DecideOnOutputLost(OnWindowsDefault, new OutputLostEventArgs(true, true), Snapshot(Speakers, Speakers));

        // Assert
        Assert.Equal(EAudioDeviceAction.Reopen, action);
    }

    [Fact(DisplayName = "output_lost_while_on_windows_default_instead_of_missing_preferred_decides_reopen")]
    public void DecideOnOutputLost_Reopens_WhenPlayingOnDefaultForMissingPreferred()
    {
        // Act
        EAudioDeviceAction action = AudioDevicePolicy.DecideOnOutputLost(OnDac, new OutputLostEventArgs(true, false), Snapshot(Headset, Headset));

        // Assert
        Assert.Equal(EAudioDeviceAction.Reopen, action);
    }

    [Theory(DisplayName = "output_lost_with_no_active_device_decides_pause")]
    [InlineData("")]
    [InlineData(Dac)]
    public void DecideOnOutputLost_Pauses_WithNoActiveDevice(string preferredId)
    {
        // Act
        EAudioDeviceAction action = AudioDevicePolicy.DecideOnOutputLost(new AudioOutputTarget(preferredId, EAudioOutputMode.Shared), new OutputLostEventArgs(true, true), AudioDeviceSnapshot.Empty);

        // Assert
        Assert.Equal(EAudioDeviceAction.Pause, action);
    }

    [Fact(DisplayName = "devices_changed_returns_to_preferred_when_it_reappears")]
    public void DecideOnDevicesChanged_Reopens_WhenPreferredReappears()
    {
        // Act
        EAudioDeviceAction action = AudioDevicePolicy.DecideOnDevicesChanged(OnDac, OpenOn(Speakers, isOnPreferred: false), Snapshot(Speakers, Speakers, Dac));

        // Assert
        Assert.Equal(EAudioDeviceAction.Reopen, action);
    }

    [Fact(DisplayName = "devices_changed_follows_new_windows_default")]
    public void DecideOnDevicesChanged_Reopens_WhenWindowsDefaultChanges()
    {
        // Act
        EAudioDeviceAction action = AudioDevicePolicy.DecideOnDevicesChanged(OnWindowsDefault, OpenOn(Speakers, isOnPreferred: true), Snapshot(Headset, Speakers, Headset));

        // Assert
        Assert.Equal(EAudioDeviceAction.Reopen, action);
    }

    [Fact(DisplayName = "devices_changed_pauses_when_the_preferred_disappears_before_the_output_fails")]
    public void DecideOnDevicesChanged_Pauses_WhenPreferredDisappearsWhileOpenOnIt()
    {
        // Act
        EAudioDeviceAction action = AudioDevicePolicy.DecideOnDevicesChanged(OnDac, OpenOn(Dac, isOnPreferred: true), Snapshot(Speakers, Speakers));

        // Assert
        Assert.Equal(EAudioDeviceAction.Pause, action);
    }

    [Fact(DisplayName = "devices_changed_follows_windows_default_while_the_preferred_is_still_missing")]
    public void DecideOnDevicesChanged_FollowsDefault_WhenPreferredWasAlreadyMissing()
    {
        // Act
        EAudioDeviceAction action = AudioDevicePolicy.DecideOnDevicesChanged(OnDac, OpenOn(Speakers, isOnPreferred: false), Snapshot(Headset, Speakers, Headset));

        // Assert
        Assert.Equal(EAudioDeviceAction.Reopen, action);
    }

    [Fact(DisplayName = "devices_changed_ignores_default_change_while_on_explicit_preferred")]
    public void DecideOnDevicesChanged_Ignores_DefaultChangeWhileOnPreferred()
    {
        // Act
        EAudioDeviceAction action = AudioDevicePolicy.DecideOnDevicesChanged(OnDac, OpenOn(Dac, isOnPreferred: true), Snapshot(Headset, Speakers, Headset, Dac));

        // Assert
        Assert.Equal(EAudioDeviceAction.None, action);
    }

    [Fact(DisplayName = "devices_changed_is_idempotent_when_nothing_changed")]
    public void DecideOnDevicesChanged_ReturnsNone_WhenAlreadyOnTheRightDevice()
    {
        // Act
        EAudioDeviceAction action = AudioDevicePolicy.DecideOnDevicesChanged(OnWindowsDefault, OpenOn(Speakers, isOnPreferred: true), Snapshot(Speakers, Speakers));

        // Assert
        Assert.Equal(EAudioDeviceAction.None, action);
    }

    [Fact(DisplayName = "devices_changed_does_nothing_while_the_output_is_released")]
    public void DecideOnDevicesChanged_ReturnsNone_WhenOutputIsClosed()
    {
        // Act
        EAudioDeviceAction action = AudioDevicePolicy.DecideOnDevicesChanged(OnDac, AudioOutputState.Closed, Snapshot(Speakers, Speakers, Dac));

        // Assert
        Assert.Equal(EAudioDeviceAction.None, action);
    }
}