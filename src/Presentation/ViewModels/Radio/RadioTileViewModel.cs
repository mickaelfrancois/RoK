using CommunityToolkit.Mvvm.ComponentModel;
using Rok.Application.Dto;
using Rok.ViewModels.Radio.Services;

namespace Rok.ViewModels.Radio;

public sealed partial class RadioTileViewModel : ObservableObject
{
    private readonly RadioPictureService _pictureService;

    public RadioStationDto Station { get; }

    [ObservableProperty]
    public partial BitmapImage? Picture { get; set; }

    public long Id => Station.Id;
    public string Name => Station.Name;
    public string StreamUrl => Station.StreamUrl;
    public string? HomepageUrl => Station.HomepageUrl;
    public string? CountryCode => Station.CountryCode;
    public string? Codec => Station.Codec;
    public int? Bitrate => Station.Bitrate;
    public string? FaviconUrl => Station.FaviconUrl;

    public RadioTileViewModel(RadioStationDto station, RadioPictureService pictureService)
    {
        Station = station;
        _pictureService = pictureService;
        ReloadPicture();
    }

    public void ReloadPicture()
    {
        Picture = _pictureService.LoadPicture(Station.Id);
    }
}
