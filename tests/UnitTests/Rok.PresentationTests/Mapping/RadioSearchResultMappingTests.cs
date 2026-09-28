using Rok.Application.Dto;
using Rok.Application.Features.Radios.Requests;
using Rok.Mapping;

namespace Rok.PresentationTests.Mapping;

public class RadioSearchResultMappingTests
{
    [Fact(DisplayName = "to_add_request_should_copy_every_field")]
    public void ToAddRequest_ShouldCopyEveryField()
    {
        // Arrange
        RadioSearchResultDto station = new("Jazz FM", "https://s/jazz", "https://jazz.example", "uuid-1", "https://jazz.example/logo.png", "gb", "AAC", 96);

        // Act
        AddRadioStationRequest request = station.ToAddRequest();

        // Assert
        Assert.Equal("Jazz FM", request.Name);
        Assert.Equal("https://s/jazz", request.StreamUrl);
        Assert.Equal("https://jazz.example", request.HomepageUrl);
        Assert.Equal("uuid-1", request.StationUuid);
        Assert.Equal("https://jazz.example/logo.png", request.FaviconUrl);
        Assert.Equal("gb", request.CountryCode);
        Assert.Equal("AAC", request.Codec);
        Assert.Equal(96, request.Bitrate);
    }
}