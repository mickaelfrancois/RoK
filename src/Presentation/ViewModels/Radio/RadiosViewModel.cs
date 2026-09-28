using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CleanArch.DevKit.Mediator;
using CleanArch.DevKit.Mediator.Results;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rok.Application.Dto;
using Rok.Application.Features.Radios.Requests;
using Rok.ViewModels.Radio.Services;

namespace Rok.ViewModels.Radio;

public sealed partial class RadiosViewModel : ObservableObject
{
    private readonly IMediator _mediator;
    private readonly RadioPictureService _pictureService;
    private readonly RadioSuggestionsService _suggestionsService;
    private readonly IResourceService _resourceService;
    private bool _suggestionsLoaded;

    public ObservableCollection<RadioTileViewModel> Stations { get; } = [];

    public ObservableCollection<RadioSearchResultDto> Suggestions { get; } = [];

    public bool HasNoData => Stations.Count == 0;

    public bool HasSuggestions => Stations.Count == 0 && Suggestions.Count > 0;

    public bool ShowEmptyMessage => HasNoData && !HasSuggestions && !IsLoadingSuggestions;

    public string SuggestionsTitle => SuggestionSource == RadioSuggestionSource.Country
        ? _resourceService.GetString("radiosSuggestionsLocalTitle")
        : _resourceService.GetString("radiosSuggestionsWorldTitle");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SuggestionsTitle))]
    public partial RadioSuggestionSource SuggestionSource { get; set; } = RadioSuggestionSource.None;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyMessage))]
    public partial bool IsLoadingSuggestions { get; set; }

    public RadiosViewModel(IMediator mediator, RadioPictureService pictureService, RadioSuggestionsService suggestionsService, IResourceService resourceService)
    {
        _mediator = mediator;
        _pictureService = pictureService;
        _suggestionsService = suggestionsService;
        _resourceService = resourceService;
        Stations.CollectionChanged += OnCollectionChanged;
        Suggestions.CollectionChanged += OnCollectionChanged;
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasNoData));
        OnPropertyChanged(nameof(HasSuggestions));
        OnPropertyChanged(nameof(ShowEmptyMessage));
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        Result<IReadOnlyList<RadioStationDto>> result = await _mediator.Send(new GetRadioStationsRequest());
        if (!result.IsSuccess) return;

        Stations.Clear();
        foreach (RadioStationDto station in result.Value)
            Stations.Add(new RadioTileViewModel(station, _pictureService));

        if (Stations.Count > 0)
        {
            Suggestions.Clear();
            return;
        }

        if (!_suggestionsLoaded)
            await LoadSuggestionsAsync();
    }

    private async Task LoadSuggestionsAsync()
    {
        IsLoadingSuggestions = true;

        try
        {
            RadioSuggestionsDto suggestions = await _suggestionsService.LoadAsync(CancellationToken.None);

            SuggestionSource = suggestions.Source;
            Suggestions.Clear();
            foreach (RadioSearchResultDto station in suggestions.Stations)
                Suggestions.Add(station);

            _suggestionsLoaded = true;
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            IsLoadingSuggestions = false;
        }
    }

    [RelayCommand]
    public Task PlayAsync(RadioTileViewModel tile) =>
        _mediator.Send(new PlayRadioStationByIdRequest { Id = tile.Id });

    [RelayCommand]
    public Task PlaySuggestionAsync(RadioSearchResultDto station) =>
        _suggestionsService.PlayAsync(station, SuggestionSource);

    [RelayCommand]
    public async Task AddSuggestionAsync(RadioSearchResultDto station)
    {
        if (await _suggestionsService.AddAsync(station, SuggestionSource))
            await LoadAsync();
    }

    [RelayCommand]
    public async Task DeleteAsync(RadioTileViewModel tile)
    {
        Result<bool> result = await _mediator.Send(new DeleteRadioStationRequest { Id = tile.Id });
        if (result.IsSuccess)
        {
            await _pictureService.DeletePictureAsync(tile.Id);
            Stations.Remove(tile);
        }
    }
}