using DiscordRPC;
using DiscordRPC.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rok.Application.Options;

namespace Rok.Infrastructure.Social;

public class DiscordRichPresenceService : IDisposable
{
    private readonly DiscordRpcClient? _client;
    private readonly ILogger<DiscordRichPresenceService> _logger;
    private readonly object _lock = new();
    private bool _isInitialized;
    private bool _disposed;

    public DiscordRichPresenceService(ILogger<DiscordRichPresenceService> logger, IOptions<DiscordOptions> discordOptions)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        string? applicationId = discordOptions?.Value?.ApplicationId;

        if (string.IsNullOrWhiteSpace(applicationId))
        {
            _logger.LogWarning("Discord ApplicationId is not configured. Discord Rich Presence will be disabled.");
            return;
        }

        try
        {
            _client = new DiscordRpcClient(applicationId)
            {
                Logger = new ConsoleLogger { Level = DiscordRPC.Logging.LogLevel.Warning }
            };

            _client.OnReady += (sender, e) =>
            {
                if (_disposed || e?.User == null) return;
                _logger.LogInformation("Discord Rich Presence connected for {User}", e.User.Username);
            };

            _client.OnError += (sender, e) =>
            {
                if (_disposed || e == null) return;
                _logger.LogError("Discord RPC error: {Message}", e.Message);
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Discord RPC client");
            _client = null;
        }
    }

    public void Initialize()
    {
        lock (_lock)
        {
            if (_disposed || _client == null)
                return;

            if (string.IsNullOrEmpty(_client.ApplicationID))
                return;

            if (_isInitialized)
                return;

            try
            {
                _client.Initialize();
                _isInitialized = true;
                _logger.LogInformation("Discord Rich Presence initialized");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Discord Rich Presence");
            }
        }
    }

    public void UpdatePresence(string trackTitle, string artistName, string albumName, TimeSpan elapsed, TimeSpan duration)
    {
        lock (_lock)
        {
            if (_disposed || _client == null || !_isInitialized)
                return;

            if (string.IsNullOrWhiteSpace(trackTitle))
                return;

            try
            {
                RichPresence presence = new()
                {
                    Details = string.IsNullOrWhiteSpace(trackTitle) ? "Unknown title" : trackTitle,
                    State = string.IsNullOrWhiteSpace(artistName) ? "Unknown Artist" : $"by {artistName}",
                    StatusDisplay = StatusDisplayType.Details,
                    Type = ActivityType.Listening,
                    Buttons =
                    [
                        new Button { Label = "Download Rok", Url = "https://apps.microsoft.com/store/detail/9NX19R28Q92S?cid=DevShareMCLPCS" }
                    ]
                };

                _client.SetPresence(presence);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while updating Discord presence");
            }
        }
    }

    public void ClearPresence()
    {
        lock (_lock)
        {
            if (_disposed || _client == null || !_isInitialized)
                return;

            try
            {
                _client.ClearPresence();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while clearing Discord presence");
            }
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        lock (_lock)
        {
            if (_disposed)
                return;

            if (disposing && _client != null)
            {
                try
                {
                    if (_isInitialized)
                    {
                        _client.ClearPresence();
                        _client.Deinitialize();
                    }
                    _client.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during Discord client disposal");
                }
            }

            _isInitialized = false;
            _disposed = true;
        }
    }
}