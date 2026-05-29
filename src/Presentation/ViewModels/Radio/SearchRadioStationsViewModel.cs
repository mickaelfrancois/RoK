using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rok.Application.Errors;
using Rok.Application.Features.Radios.Requests;
using Rok.ViewModels.Radio.Services;
using Windows.ApplicationModel.Resources;

namespace Rok.ViewModels.Radio;

public enum SearchFeedbackKind
{
    None,
    Success,
    Info,
    Error
}

public sealed partial class SearchRadioStationsViewModel : ObservableObject
{
    private readonly IMediator _mediator;
    private readonly ResourceLoader _resourceLoader;
    private readonly RadioPictureService _pictureService;

    [ObservableProperty]
    public partial string Query { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoResults))]
    public partial bool IsSearching { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    [NotifyPropertyChangedFor(nameof(HasNoResults))]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoResults))]
    public partial bool HasSearched { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFeedback))]
    public partial string? FeedbackMessage { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFeedback))]
    public partial SearchFeedbackKind FeedbackKind { get; set; } = SearchFeedbackKind.None;

    public ObservableCollection<RadioSearchResultDto> Results { get; } = [];

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool HasFeedback => !string.IsNullOrEmpty(FeedbackMessage);

    public bool HasNoResults => HasSearched && !IsSearching && Results.Count == 0 && !HasError;

    public SearchRadioStationsViewModel(IMediator mediator, ResourceLoader resourceLoader, RadioPictureService pictureService)
    {
        _mediator = mediator;
        _resourceLoader = resourceLoader;
        _pictureService = pictureService;
        Results.CollectionChanged += OnResultsChanged;
    }

    private void OnResultsChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        OnPropertyChanged(nameof(HasNoResults));

    [RelayCommand(CanExecute = nameof(CanSearch))]
    private async Task SearchAsync(CancellationToken ct)
    {
        IsSearching = true;
        ErrorMessage = null;
        FeedbackMessage = null;
        FeedbackKind = SearchFeedbackKind.None;
        Results.Clear();

        try
        {
            Result<IReadOnlyList<RadioSearchResultDto>> result =
                await _mediator.Send(
                    new SearchRadioStationsRequest { Query = Query.Trim(), Limit = 50 },
                    ct);

            HasSearched = true;

            if (result.IsFailure)
            {
                ErrorMessage = ResolveErrorMessage(result.Errors.First());
                return;
            }

            foreach (RadioSearchResultDto r in result.Value)
                Results.Add(r);
        }
        finally
        {
            IsSearching = false;
        }
    }

    private bool CanSearch() => !IsSearching && Query?.Trim().Length >= 2;

    [RelayCommand]
    private Task PlayAsync(RadioSearchResultDto r) =>
        _mediator.Send(new PlayRadioUrlRequest { Url = r.StreamUrl });

    [RelayCommand]
    private async Task AddToFavoritesAsync(RadioSearchResultDto r)
    {
        Result<long> result = await _mediator.Send(new AddRadioStationRequest
        {
            Name = r.Name,
            StreamUrl = r.StreamUrl,
            HomepageUrl = r.HomepageUrl,
            StationUuid = r.StationUuid,
            FaviconUrl = r.FaviconUrl,
            CountryCode = r.CountryCode,
            Codec = r.Codec,
            Bitrate = r.Bitrate,
        });

        if (result.IsSuccess)
        {
            SetFeedback(_resourceLoader.GetString("radioFavoriteAdded"), SearchFeedbackKind.Success);
            _ = DownloadPictureInBackgroundAsync(result.Value, r.FaviconUrl);
        }
        else if (result.Errors.FirstOrDefault() is ConflictError)
        {
            SetFeedback(_resourceLoader.GetString("radioFavoriteDuplicate"), SearchFeedbackKind.Info);
        }
        else
        {
            SetFeedback(ResolveErrorMessage(result.Errors.First()), SearchFeedbackKind.Error);
        }
    }

    private async Task DownloadPictureInBackgroundAsync(long stationId, string? faviconUrl)
    {
        if (string.IsNullOrWhiteSpace(faviconUrl))
            return;

        await _pictureService.DownloadAndSaveAsync(stationId, faviconUrl);
    }

    private void SetFeedback(string message, SearchFeedbackKind kind)
    {
        FeedbackMessage = message;
        FeedbackKind = kind;
    }

    public void ClearFeedback()
    {
        FeedbackMessage = null;
        FeedbackKind = SearchFeedbackKind.None;
    }

    private string ResolveErrorMessage(Error error)
    {
        string localized = _resourceLoader.GetString($"error.{error.Code}");
        return string.IsNullOrEmpty(localized) ? error.Message : localized;
    }
}