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

    public ObservableCollection<RadioTileViewModel> Stations { get; } = [];

    public bool HasNoData => Stations.Count == 0;

    public RadiosViewModel(IMediator mediator, RadioPictureService pictureService)
    {
        _mediator = mediator;
        _pictureService = pictureService;
        Stations.CollectionChanged += OnStationsChanged;
    }

    private void OnStationsChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        OnPropertyChanged(nameof(HasNoData));

    [RelayCommand]
    public async Task LoadAsync()
    {
        Result<IReadOnlyList<RadioStationDto>> result = await _mediator.Send(new GetRadioStationsRequest());
        if (!result.IsSuccess) return;

        Stations.Clear();
        foreach (RadioStationDto station in result.Value)
            Stations.Add(new RadioTileViewModel(station, _pictureService));
    }

    [RelayCommand]
    public Task PlayAsync(RadioTileViewModel tile) =>
        _mediator.Send(new PlayRadioStationByIdRequest { Id = tile.Id });

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
