using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
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

    // New Teams (and other WebView2-hosted apps) render the live call audio through a
    // msedgewebview2.exe child process rather than the main app process. Such a session is
    // treated as communication audio only when one of its ancestor processes is a known
    // communication app, so standalone Edge / unrelated WebView2 apps are not affected.
    private const string WebViewHostProcessName = "msedgewebview2";

    private const int MaxAncestorDepth = 6;

    private readonly ILogger<CallDetectionService> _logger;

    private readonly object _gate = new();

    private MMDeviceEnumerator? _enumerator;
    private readonly List<MMDevice> _trackedDevices = new();
    private readonly List<AudioSessionManager> _trackedManagers = new();
    private readonly List<TrackedSession> _trackedSessions = new();
    private bool _callActive;

    public event EventHandler<bool>? CallStateChanged;

    public CallDetectionService(ILogger<CallDetectionService> logger)
    {
        _logger = logger;
    }

    public void Start()
    {
        _enumerator = new MMDeviceEnumerator();
        ScanAllEndpoints();
    }


    public void Stop()
    {
        lock (_gate)
        {
            foreach (TrackedSession tracked in _trackedSessions)
                UnregisterSafely(tracked.Session);

            foreach (AudioSessionManager manager in _trackedManagers)
                manager.OnSessionCreated -= OnSessionCreated;

            _trackedSessions.Clear();
            _trackedManagers.Clear();
            _trackedDevices.Clear();
            _enumerator?.Dispose();
        }
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

                    _trackedManagers.Add(sessionManager);

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
        string? processName = ResolveCommunicationProcess(session);

        if (processName is null)
            return;

        lock (_gate)
        {
            session.RegisterEventClient(this);
            _trackedSessions.Add(new TrackedSession(session, processName));
        }

        EvaluateCallState();
    }

    private static string? ResolveCommunicationProcess(AudioSessionControl session)
    {
        uint processId;

        try
        {
            processId = session.GetProcessID;
        }
        catch
        {
            return null;
        }

        string processName;

        try
        {
            using Process process = Process.GetProcessById((int)processId);
            processName = process.ProcessName;
        }
        catch
        {
            return null;
        }

        if (CommunicationApps.Any(app => processName.Contains(app, StringComparison.OrdinalIgnoreCase)))
            return processName;

        if (processName.Contains(WebViewHostProcessName, StringComparison.OrdinalIgnoreCase)
            && HasCommunicationAncestor(processId))
            return processName;

        return null;
    }

    private static bool HasCommunicationAncestor(uint processId)
    {
        int currentPid = (int)processId;
        HashSet<int> visited = new();

        for (int depth = 0; depth < MaxAncestorDepth; depth++)
        {
            if (!visited.Add(currentPid))
                break;

            if (!TryGetParentProcessId(currentPid, out int parentPid) || parentPid <= 0)
                break;

            string parentName;

            try
            {
                using Process parent = Process.GetProcessById(parentPid);
                parentName = parent.ProcessName;
            }
            catch
            {
                break;
            }

            if (CommunicationApps.Any(app => parentName.Contains(app, StringComparison.OrdinalIgnoreCase)))
                return true;

            currentPid = parentPid;
        }

        return false;
    }

    private void EvaluateCallState()
    {
        List<TrackedSession> deadSessions = new();
        bool anyActive = false;
        string? activeProcess = null;

        lock (_gate)
        {
            foreach (TrackedSession tracked in _trackedSessions)
            {
                try
                {
                    if (tracked.Session.State == AudioSessionState.AudioSessionStateActive)
                    {
                        anyActive = true;
                        activeProcess ??= tracked.ProcessName;
                    }
                }
                catch (COMException)
                {
                    deadSessions.Add(tracked);
                }
            }

            foreach (TrackedSession dead in deadSessions)
            {
                UnregisterSafely(dead.Session);
                _trackedSessions.Remove(dead);
            }
        }

        if (anyActive == _callActive)
            return;

        _callActive = anyActive;

        _logger.LogInformation("Call detection: in call = {InCall} (process: {Process})", _callActive, activeProcess ?? "none");

        CallStateChanged?.Invoke(this, _callActive);
    }

    private void UnregisterSafely(AudioSessionControl session)
    {
        try
        {
            session.UnRegisterEventClient(this);
        }
        catch
        {
            /* Session may already be gone */
        }
    }

    private static bool TryGetParentProcessId(int processId, out int parentProcessId)
    {
        parentProcessId = 0;

        try
        {
            using Process process = Process.GetProcessById(processId);

            ProcessBasicInformation info = default;
            int status = NtQueryInformationProcess(process.Handle, 0, ref info, Marshal.SizeOf<ProcessBasicInformation>(), out _);

            if (status != 0)
                return false;

            parentProcessId = info.InheritedFromUniqueProcessId.ToInt32();
            return parentProcessId > 0;
        }
        catch
        {
            return false;
        }
    }

    void IAudioSessionEventsHandler.OnStateChanged(AudioSessionState state) => EvaluateCallState();
    void IAudioSessionEventsHandler.OnSessionDisconnected(AudioSessionDisconnectReason disconnectReason) => EvaluateCallState();
    void IAudioSessionEventsHandler.OnVolumeChanged(float volume, bool isMuted) { }
    void IAudioSessionEventsHandler.OnDisplayNameChanged(string displayName) { }
    void IAudioSessionEventsHandler.OnIconPathChanged(string iconPath) { }
    void IAudioSessionEventsHandler.OnChannelVolumeChanged(uint channelCount, IntPtr newVolumes, uint channelIndex) { }
    void IAudioSessionEventsHandler.OnGroupingParamChanged(ref Guid groupingId) { }

    public void Dispose() => Stop();

    private readonly record struct TrackedSession(AudioSessionControl Session, string ProcessName);

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessBasicInformation
    {
        public IntPtr Reserved1;
        public IntPtr PebBaseAddress;
        public IntPtr Reserved2_0;
        public IntPtr Reserved2_1;
        public IntPtr UniqueProcessId;
        public IntPtr InheritedFromUniqueProcessId;
    }

    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(IntPtr processHandle, int processInformationClass, ref ProcessBasicInformation processInformation, int processInformationLength, out int returnLength);
}
