namespace Rok.Application.Player.Output;

/// <summary>Chooses the output device and decides how the player reacts to device events.</summary>
public static class AudioDevicePolicy
{
    /// <summary>
    /// The chosen device when it is active, otherwise the Windows default device; <c>null</c> when no device is available.
    /// </summary>
    public static string? ResolveDeviceId(string? preferredDeviceId, AudioDeviceSnapshot snapshot)
    {
        if (snapshot.IsActive(preferredDeviceId))
            return preferredDeviceId;

        return snapshot.DefaultId;
    }

    /// <summary>
    /// Pauses when the explicitly chosen device disappears, so the sound never moves to another device on its own.
    /// Reopens after a transient failure, and when following the Windows default device.
    /// </summary>
    public static EAudioDeviceAction DecideOnOutputLost(AudioOutputTarget target, OutputLostEventArgs lost, AudioDeviceSnapshot snapshot)
    {
        if (ResolveDeviceId(target.PreferredDeviceId, snapshot) is null)
            return EAudioDeviceAction.Pause;

        if (target.FollowsWindowsDefault || !lost.WasOnPreferred)
            return EAudioDeviceAction.Reopen;

        return snapshot.IsActive(target.PreferredDeviceId)
            ? EAudioDeviceAction.Reopen
            : EAudioDeviceAction.Pause;
    }

    /// <summary>
    /// Moves back to the chosen device once it is active again, pauses when the chosen device disappears while the output
    /// still plays on it (the notification may arrive before the output fails), and follows the Windows default device
    /// when no device is chosen or the chosen one was already missing. Idempotent: a burst of identical notifications
    /// yields a single action.
    /// </summary>
    public static EAudioDeviceAction DecideOnDevicesChanged(AudioOutputTarget target, AudioOutputState state, AudioDeviceSnapshot snapshot)
    {
        if (!state.IsOpen)
            return EAudioDeviceAction.None;

        if (!target.FollowsWindowsDefault)
        {
            if (snapshot.IsActive(target.PreferredDeviceId))
                return state.IsOnPreferred ? EAudioDeviceAction.None : EAudioDeviceAction.Reopen;

            if (state.IsOnPreferred)
                return EAudioDeviceAction.Pause;
        }

        if (snapshot.DefaultId is not null && state.DeviceId != snapshot.DefaultId)
            return EAudioDeviceAction.Reopen;

        return EAudioDeviceAction.None;
    }
}