using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Dto;
using Rok.Application.Features.Artists.Requests;
using Rok.Services;
using Rok.ViewModels.Artist.Services;

namespace Rok.PresentationTests.ViewModels.Artist.Services;

public class ArtistEditServiceTests
{
    private readonly FakeMediator _mediator = new();

    private readonly Mock<IDialogService> _dialogService = new();

    private ArtistEditService BuildService() => new(_mediator, _dialogService.Object, NullLogger<ArtistEditService>.Instance);

    [Fact(DisplayName = "UpdateFavoriteAsync should send the favorite command and update the artist state")]
    public async Task UpdateFavoriteAsync_ShouldSendCommandAndUpdateState()
    {
        // Arrange
        ArtistDto artist = new() { Id = 5, IsFavorite = false };
        ArtistEditService sut = BuildService();

        // Act
        await sut.UpdateFavoriteAsync(artist, isFavorite: true);

        // Assert
        Assert.True(artist.IsFavorite);
        UpdateArtistFavoriteRequest sent = Assert.Single(_mediator.Sent<UpdateArtistFavoriteRequest>());
        Assert.Equal(5, sent.Id);
        Assert.True(sent.IsFavorite);
    }

    [Fact(DisplayName = "UpdateTagsAsync should send the tags command for the given artist")]
    public async Task UpdateTagsAsync_ShouldSendTagsCommand()
    {
        // Arrange
        ArtistEditService sut = BuildService();
        string[] tags = new[] { "rock", "indie" };

        // Act
        await sut.UpdateTagsAsync(id: 5, tags);

        // Assert
        UpdateArtistTagsRequest sent = Assert.Single(_mediator.Sent<UpdateArtistTagsRequest>());
        Assert.Equal(5, sent.Id);
        Assert.True(sent.Tags.SequenceEqual(tags));
    }

    [Fact(DisplayName = "UpdatePictureDominantColorAsync should send the color command")]
    public async Task UpdatePictureDominantColorAsync_ShouldSendCommand()
    {
        // Arrange
        ArtistEditService sut = BuildService();

        // Act
        await sut.UpdatePictureDominantColorAsync(id: 5, colorValue: 0xFF112233);

        // Assert
        UpdateArtistPictureDominantColorRequest sent = Assert.Single(_mediator.Sent<UpdateArtistPictureDominantColorRequest>());
        Assert.Equal(5, sent.Id);
        Assert.Equal(0xFF112233, sent.ColorValue);
    }

    [Theory(DisplayName = "OpenOfficialSiteAsync should return false when no URL is available on the artist")]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData(" ", " ")]
    public async Task OpenOfficialSiteAsync_ShouldReturnFalse_WhenNoUrl(string? officialSite, string? wikipedia)
    {
        // Arrange
        ArtistDto artist = new() { Id = 1, Name = "X", OfficialSiteUrl = officialSite!, WikipediaUrl = wikipedia! };
        ArtistEditService sut = BuildService();

        // Act
        bool result = await sut.OpenOfficialSiteAsync(artist);

        // Assert
        Assert.False(result);
    }

    [Fact(DisplayName = "EditArtistAsync confirmed should mutate the artist with the edited values")]
    public async Task EditArtistAsync_Confirmed_ShouldMutateArtist()
    {
        // Arrange
        ArtistDto artist = new()
        {
            Id = 7,
            MusicBrainzID = "old-mb",
            FormedYear = 1980,
            BornYear = 1955,
            DiedYear = null,
            Disbanded = false,
            Members = "old members",
            Biography = "old bio"
        };

        ArtistEditValues edited = new()
        {
            MusicBrainzID = "new-mb",
            FormedYear = "1990",
            BornYear = "1960",
            DiedYear = "2020",
            Disbanded = true,
            Members = "new members",
            Biography = "new bio"
        };

        SetupDialog(edited);
        ArtistEditService sut = BuildService();

        // Act
        bool result = await sut.EditArtistAsync(artist);

        // Assert
        Assert.True(result);
        Assert.Equal("new-mb", artist.MusicBrainzID);
        Assert.Equal(1990, artist.FormedYear);
        Assert.Equal(1960, artist.BornYear);
        Assert.Equal(2020, artist.DiedYear);
        Assert.True(artist.Disbanded);
        Assert.Equal("new members", artist.Members);
        Assert.Equal("new bio", artist.Biography);
    }

    [Fact(DisplayName = "EditArtistAsync confirmed should send an update command with the edited fields")]
    public async Task EditArtistAsync_Confirmed_ShouldSendUpdateCommand()
    {
        // Arrange
        ArtistDto artist = new() { Id = 7 };

        ArtistEditValues edited = new()
        {
            MusicBrainzID = "mb",
            FormedYear = "1990",
            BornYear = "1960",
            DiedYear = "2020",
            Disbanded = true,
            Members = "members",
            Biography = "bio"
        };

        SetupDialog(edited);
        ArtistEditService sut = BuildService();

        // Act
        await sut.EditArtistAsync(artist);

        // Assert
        UpdateArtistRequest sent = Assert.Single(_mediator.Sent<UpdateArtistRequest>());
        Assert.Equal(7, sent.Id);
        Assert.Equal("mb", sent.MusicBrainzID);
        Assert.Equal(1990, sent.FormedYear);
        Assert.Equal(1960, sent.BornYear);
        Assert.Equal(2020, sent.DiedYear);
        Assert.True(sent.Disbanded);
        Assert.Equal("members", sent.Members);
        Assert.Equal("bio", sent.Biography);
    }

    [Fact(DisplayName = "EditArtistAsync confirmed should preserve fields not shown in the dialog (social URLs)")]
    public async Task EditArtistAsync_Confirmed_ShouldPreserveHiddenFields()
    {
        // Arrange - a URL that the dialog never edits must survive the round-trip via ToCommand().
        ArtistDto artist = new()
        {
            Id = 7,
            FacebookUrl = "https://facebook.com/band",
            WikipediaUrl = "https://wikipedia.org/band",
            SimilarArtists = "Some Other Band"
        };

        SetupDialog(new ArtistEditValues { Biography = "bio" });
        ArtistEditService sut = BuildService();

        // Act
        await sut.EditArtistAsync(artist);

        // Assert
        UpdateArtistRequest sent = Assert.Single(_mediator.Sent<UpdateArtistRequest>());
        Assert.Equal("https://facebook.com/band", sent.FacebookUrl);
        Assert.Equal("https://wikipedia.org/band", sent.WikipediaUrl);
        Assert.Equal("Some Other Band", sent.SimilarArtists);
    }

    [Fact(DisplayName = "EditArtistAsync confirmed with a cleared biography should send an empty biography")]
    public async Task EditArtistAsync_Confirmed_ClearedBiography_ShouldSendEmpty()
    {
        // Arrange
        ArtistDto artist = new() { Id = 7, Biography = "old bio" };

        SetupDialog(new ArtistEditValues { Biography = "" });
        ArtistEditService sut = BuildService();

        // Act
        await sut.EditArtistAsync(artist);

        // Assert
        UpdateArtistRequest sent = Assert.Single(_mediator.Sent<UpdateArtistRequest>());
        Assert.True(string.IsNullOrEmpty(sent.Biography));
        Assert.True(string.IsNullOrEmpty(artist.Biography));
    }

    [Theory(DisplayName = "EditArtistAsync should map an empty or non-numeric year to null")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData(null)]
    public async Task EditArtistAsync_NonNumericYear_ShouldMapToNull(string? yearInput)
    {
        // Arrange
        ArtistDto artist = new() { Id = 7, FormedYear = 1980 };

        SetupDialog(new ArtistEditValues { FormedYear = yearInput });
        ArtistEditService sut = BuildService();

        // Act
        await sut.EditArtistAsync(artist);

        // Assert
        UpdateArtistRequest sent = Assert.Single(_mediator.Sent<UpdateArtistRequest>());
        Assert.Null(sent.FormedYear);
        Assert.Null(artist.FormedYear);
    }

    [Fact(DisplayName = "EditArtistAsync cancelled should not send any command nor mutate the artist")]
    public async Task EditArtistAsync_Cancelled_ShouldDoNothing()
    {
        // Arrange
        ArtistDto artist = new() { Id = 7, Biography = "old bio", FormedYear = 1980 };

        _dialogService
            .Setup(x => x.ShowEditArtistAsync(It.IsAny<ArtistEditValues>()))
            .ReturnsAsync((ArtistEditValues?)null);

        ArtistEditService sut = BuildService();

        // Act
        bool result = await sut.EditArtistAsync(artist);

        // Assert
        Assert.False(result);
        Assert.Empty(_mediator.Sent<UpdateArtistRequest>());
        Assert.Equal("old bio", artist.Biography);
        Assert.Equal(1980, artist.FormedYear);
    }

    private void SetupDialog(ArtistEditValues edited)
    {
        _dialogService
            .Setup(x => x.ShowEditArtistAsync(It.IsAny<ArtistEditValues>()))
            .ReturnsAsync(edited);
    }
}