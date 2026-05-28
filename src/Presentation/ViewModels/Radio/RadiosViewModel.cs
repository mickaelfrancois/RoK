using System.Collections.ObjectModel;
using CleanArch.DevKit.Mediator;
using CleanArch.DevKit.Mediator.Results;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rok.Application.Dto;
using Rok.Application.Features.Radios.Requests;

namespace Rok.ViewModels.Radio;

public sealed partial class RadiosViewModel(IMediator mediator) : ObservableObject
{
    public ObservableCollection<RadioStationDto> Stations { get; } = [];

    [RelayCommand]
    public async Task LoadAsync()
    {
        Result<IReadOnlyList<RadioStationDto>> result = await mediator.Send(new GetRadioStationsRequest());
        if (!result.IsSuccess) return;

        Stations.Clear();
        foreach (RadioStationDto station in result.Value)
            Stations.Add(station);
    }

    [RelayCommand]
    public Task PlayAsync(RadioStationDto station) =>
        mediator.Send(new PlayRadioStationByIdRequest { Id = station.Id });


    [RelayCommand]
    public async Task DeleteAsync(RadioStationDto station)
    {
        Result<bool> result = await mediator.Send(new DeleteRadioStationRequest { Id = station.Id });
        if (result.IsSuccess)
            Stations.Remove(station);
    }
}
