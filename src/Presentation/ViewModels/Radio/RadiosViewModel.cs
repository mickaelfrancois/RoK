using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CleanArch.DevKit.Mediator;
using CleanArch.DevKit.Mediator.Results;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rok.Application.Dto;
using Rok.Application.Features.Radios.Requests;

namespace Rok.ViewModels.Radio;

public sealed partial class RadiosViewModel : ObservableObject
{
    private readonly IMediator _mediator;

    public ObservableCollection<RadioStationDto> Stations { get; } = [];

    public bool HasNoData => Stations.Count == 0;

    public RadiosViewModel(IMediator mediator)
    {
        _mediator = mediator;
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
            Stations.Add(station);
    }

    [RelayCommand]
    public Task PlayAsync(RadioStationDto station) =>
        _mediator.Send(new PlayRadioStationByIdRequest { Id = station.Id });

    [RelayCommand]
    public async Task DeleteAsync(RadioStationDto station)
    {
        Result<bool> result = await _mediator.Send(new DeleteRadioStationRequest { Id = station.Id });
        if (result.IsSuccess)
            Stations.Remove(station);
    }
}
