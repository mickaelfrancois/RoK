using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rok.Application.Features.EqualizerPresets.Command;
using Rok.Domain.Enums;

namespace Rok.ViewModels.Player;

public sealed partial class EqualizerViewModel : ObservableObject
{
    private readonly IPlayerEngine _playerEngine;
    private readonly IEqualizerPresetResolver _resolver;
    private readonly IMediator _mediator;
    private readonly ILogger<EqualizerViewModel> _logger;

    private TrackDto? _currentTrack;
    private bool _suppressClearBuiltinPreset;

    public EqualizerViewModel(IPlayerEngine playerEngine, IEqualizerPresetResolver resolver, IMediator mediator, ILogger<EqualizerViewModel> logger)
    {
        _playerEngine = playerEngine;
        _resolver = resolver;
        _mediator = mediator;
        _logger = logger;
    }

    private EqualizerScope _activePresetScope;
    public EqualizerScope ActivePresetScope
    {
        get => _activePresetScope;
        private set
        {
            if (SetProperty(ref _activePresetScope, value))
                OnPropertyChanged(nameof(CanRemoveActivePreset));
        }
    }

    private EqualizerBuiltinPreset? _activeBuiltinPreset;
    public EqualizerBuiltinPreset? ActiveBuiltinPreset
    {
        get => _activeBuiltinPreset;
        private set => SetProperty(ref _activeBuiltinPreset, value);
    }

    public bool CanSaveForTrack => _currentTrack != null;
    public bool CanSaveForAlbum => _currentTrack?.AlbumId != null;
    public bool CanSaveForArtist => _currentTrack?.ArtistId != null;
    public bool CanSaveForGenre => _currentTrack?.GenreId != null;
    public bool CanRemoveActivePreset => _activePresetScope != EqualizerScope.Default;

    private float _band32Hz;
    public float Band32Hz
    {
        get => _band32Hz;
        set { if (SetProperty(ref _band32Hz, value)) { _playerEngine.SetEqualizerBand(0, value); ClearBuiltinPreset(); } }
    }

    private float _band64Hz;
    public float Band64Hz
    {
        get => _band64Hz;
        set { if (SetProperty(ref _band64Hz, value)) { _playerEngine.SetEqualizerBand(1, value); ClearBuiltinPreset(); } }
    }

    private float _band125Hz;
    public float Band125Hz
    {
        get => _band125Hz;
        set { if (SetProperty(ref _band125Hz, value)) { _playerEngine.SetEqualizerBand(2, value); ClearBuiltinPreset(); } }
    }

    private float _band250Hz;
    public float Band250Hz
    {
        get => _band250Hz;
        set { if (SetProperty(ref _band250Hz, value)) { _playerEngine.SetEqualizerBand(3, value); ClearBuiltinPreset(); } }
    }

    private float _band500Hz;
    public float Band500Hz
    {
        get => _band500Hz;
        set { if (SetProperty(ref _band500Hz, value)) { _playerEngine.SetEqualizerBand(4, value); ClearBuiltinPreset(); } }
    }

    private float _band1kHz;
    public float Band1kHz
    {
        get => _band1kHz;
        set { if (SetProperty(ref _band1kHz, value)) { _playerEngine.SetEqualizerBand(5, value); ClearBuiltinPreset(); } }
    }

    private float _band2kHz;
    public float Band2kHz
    {
        get => _band2kHz;
        set { if (SetProperty(ref _band2kHz, value)) { _playerEngine.SetEqualizerBand(6, value); ClearBuiltinPreset(); } }
    }

    private float _band4kHz;
    public float Band4kHz
    {
        get => _band4kHz;
        set { if (SetProperty(ref _band4kHz, value)) { _playerEngine.SetEqualizerBand(7, value); ClearBuiltinPreset(); } }
    }

    private float _band8kHz;
    public float Band8kHz
    {
        get => _band8kHz;
        set { if (SetProperty(ref _band8kHz, value)) { _playerEngine.SetEqualizerBand(8, value); ClearBuiltinPreset(); } }
    }

    private float _band16kHz;
    public float Band16kHz
    {
        get => _band16kHz;
        set { if (SetProperty(ref _band16kHz, value)) { _playerEngine.SetEqualizerBand(9, value); ClearBuiltinPreset(); } }
    }

    [RelayCommand]
    public async Task SavePresetAsync(EqualizerScope scope)
    {
        if (_currentTrack is null)
            return;

        long? scopeId = scope switch
        {
            EqualizerScope.Track => _currentTrack.Id,
            EqualizerScope.Album => _currentTrack.AlbumId,
            EqualizerScope.Artist => _currentTrack.ArtistId,
            EqualizerScope.Genre => _currentTrack.GenreId,
            _ => null
        };

        await _mediator.SendMessageAsync(new SaveEqualizerPresetCommand
        {
            Scope = scope,
            ScopeId = scopeId,
            Bands = GetCurrentBands()
        });

        ActivePresetScope = scope;
    }

    [RelayCommand]
    public async Task RemovePresetAsync()
    {
        if (_currentTrack is null || _activePresetScope == EqualizerScope.Default)
            return;

        long? scopeId = _activePresetScope switch
        {
            EqualizerScope.Track => _currentTrack.Id,
            EqualizerScope.Album => _currentTrack.AlbumId,
            EqualizerScope.Artist => _currentTrack.ArtistId,
            EqualizerScope.Genre => _currentTrack.GenreId,
            _ => null
        };

        await _mediator.SendMessageAsync(new DeleteEqualizerPresetCommand
        {
            Scope = _activePresetScope,
            ScopeId = scopeId
        });

        await ApplyPresetAsync(_currentTrack);
    }

    [RelayCommand]
    public async Task ApplyBuiltinPresetAsync(EqualizerBuiltinPreset preset)
    {
        _suppressClearBuiltinPreset = true;
        Apply(preset.Bands);
        _suppressClearBuiltinPreset = false;
        ActiveBuiltinPreset = preset;

        if (_activePresetScope == EqualizerScope.Default)
            await _mediator.SendMessageAsync(new SaveEqualizerPresetCommand
            {
                Scope = EqualizerScope.Default,
                ScopeId = null,
                BuiltinPresetKey = preset.Key,
                Bands = GetCurrentBands()
            });
    }

    [RelayCommand]
    public void Reset()
    {
        _suppressClearBuiltinPreset = true;
        Apply(EqualizerBuiltinPresets.Flat.Bands);
        _suppressClearBuiltinPreset = false;
        ActiveBuiltinPreset = EqualizerBuiltinPresets.Flat;
        ActivePresetScope = EqualizerScope.Default;
    }

    public async Task ApplyPresetAsync(TrackDto track)
    {
        _currentTrack = track;

        EqualizerPresetDto? preset = await _resolver.ResolveAsync(track);

        if (preset is not null)
            _logger.LogInformation("Applying equalizer preset (Scope: {Scope}, ScopeId: {ScopeId}) for track '{TrackTitle}'", preset.Scope, preset.ScopeId, track.Title);

        float[] bands = preset?.Bands ?? new float[10];

        _suppressClearBuiltinPreset = true;
        Apply(bands);
        _suppressClearBuiltinPreset = false;

        EqualizerScope resolvedScope = preset?.Scope ?? EqualizerScope.Default;
        ActivePresetScope = resolvedScope;
        ActiveBuiltinPreset = resolvedScope == EqualizerScope.Default && preset?.BuiltinPresetKey is not null
            ? EqualizerBuiltinPresets.All.FirstOrDefault(p => p.Key == preset.BuiltinPresetKey)
            : null;

        OnPropertyChanged(nameof(CanSaveForTrack));
        OnPropertyChanged(nameof(CanSaveForAlbum));
        OnPropertyChanged(nameof(CanSaveForArtist));
        OnPropertyChanged(nameof(CanSaveForGenre));
    }

    private void ClearBuiltinPreset()
    {
        if (!_suppressClearBuiltinPreset && _activeBuiltinPreset is not null)
            ActiveBuiltinPreset = null;
    }

    private void Apply(float[] bands)
    {
        Band32Hz = bands[0];
        Band64Hz = bands[1];
        Band125Hz = bands[2];
        Band250Hz = bands[3];
        Band500Hz = bands[4];
        Band1kHz = bands[5];
        Band2kHz = bands[6];
        Band4kHz = bands[7];
        Band8kHz = bands[8];
        Band16kHz = bands[9];
    }

    private float[] GetCurrentBands() => new float[]
    {
        _band32Hz, _band64Hz, _band125Hz, _band250Hz, _band500Hz,
        _band1kHz, _band2kHz, _band4kHz, _band8kHz, _band16kHz
    };
}
