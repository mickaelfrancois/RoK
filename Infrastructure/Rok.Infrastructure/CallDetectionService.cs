using System.Diagnostics;
using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using Rok.Application.Interfaces;

namespace Rok.Infrastructure;

public sealed class CallDetectionService : ICallDetectionService, IAudioSessionEventsHandler, IDisposable
{
    private static readonly HashSet<string> CommunicationApps = new(StringComparer.OrdinalIgnoreCase)
    {
        "Teams", "ms-teams", "Discord", "Slack", "zoom", "webex", "skype"
    };

    private MMDeviceEnumerator? _enumerator;
    private readonly List<MMDevice> _trackedDevices = new();
    private readonly List<AudioSessionControl> _trackedSessions = new();
    private readonly List<AudioSessionManager> _trackedManagers = new();
    private bool _callActive;

    public event EventHandler<bool>? CallStateChanged;

    public void Start()
    {
        _enumerator = new MMDeviceEnumerator();
        ScanAllEndpoints();
    }


    public void Stop()
    {
        foreach (AudioSessionControl session in _trackedSessions)
            session.UnRegisterEventClient(this);

        foreach (AudioSessionManager manager in _trackedManagers)
            manager.OnSessionCreated -= OnSessionCreated;

        _trackedSessions.Clear();
        _trackedManagers.Clear();
        _trackedDevices.Clear();
        _enumerator?.Dispose();
    }


    private void ScanAllEndpoints()
    {
        ReadOnlySpan<DataFlow> dataFlows = stackalloc DataFlow[] { DataFlow.Render, DataFlow.Capture };

        foreach (DataFlow dataFlow in dataFlows)
        {
            MMDeviceCollection devices = _enumerator!.EnumerateAudioEndPoints(dataFlow, DeviceState.Active);

            foreach (MMDevice device in devices)
            {
                try
                {
                    _trackedDevices.Add(device);

                    AudioSessionManager sessionManager = device.AudioSessionManager;
                    sessionManager.OnSessionCreated += OnSessionCreated;

                    _trackedManagers.Clear();

                    ScanExistingSessions(sessionManager);
                }
                catch
                {
                    /* Ignore devices we can't access */
                }
            }
        }
    }

    private void ScanExistingSessions(AudioSessionManager manager)
    {
        SessionCollection sessions = manager.Sessions;
        for (int i = 0; i < sessions.Count; i++)
            TrackSession(sessions[i]);
    }

    private void OnSessionCreated(object sender, IAudioSessionControl newSession)
    {
        AudioSessionControl control = new(newSession);
        TrackSession(control);
    }

    private void TrackSession(AudioSessionControl session)
    {
        if (!IsCommunicationApp(session))
            return;

        session.RegisterEventClient(this);
        _trackedSessions.Add(session);

        EvaluateCallState();
    }

    private static bool IsCommunicationApp(AudioSessionControl session)
    {
        try
        {
            using Process process = Process.GetProcessById((int)session.GetProcessID);
            return CommunicationApps.Any(app =>
                process.ProcessName.Contains(app, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    private void EvaluateCallState()
    {
        List<AudioSessionControl> deadSessions = new();
        bool anyActive = false;

        foreach (AudioSessionControl session in _trackedSessions)
        {
            try
            {
                if (session.State == AudioSessionState.AudioSessionStateActive)
                    anyActive = true;
            }
            catch (COMException)
            {
                deadSessions.Add(session);
            }
        }

        foreach (AudioSessionControl dead in deadSessions)
        {
            dead.UnRegisterEventClient(this);
            _trackedSessions.Remove(dead);
        }

        if (anyActive == _callActive)
            return;

        _callActive = anyActive;
        CallStateChanged?.Invoke(this, _callActive);
    }

    void IAudioSessionEventsHandler.OnStateChanged(AudioSessionState state) => EvaluateCallState();
    void IAudioSessionEventsHandler.OnSessionDisconnected(AudioSessionDisconnectReason disconnectReason) => EvaluateCallState();
    void IAudioSessionEventsHandler.OnVolumeChanged(float volume, bool isMuted) { }
    void IAudioSessionEventsHandler.OnDisplayNameChanged(string displayName) { }
    void IAudioSessionEventsHandler.OnIconPathChanged(string iconPath) { }
    void IAudioSessionEventsHandler.OnChannelVolumeChanged(uint channelCount, IntPtr newVolumes, uint channelIndex) { }
    void IAudioSessionEventsHandler.OnGroupingParamChanged(ref Guid groupingId) { }

    public void Dispose() => Stop();
}