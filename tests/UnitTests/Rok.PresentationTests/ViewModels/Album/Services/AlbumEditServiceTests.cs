using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Dto;
using Rok.Application.Features.Albums.Requests;
using Rok.Application.Interfaces;
using Rok.Services;
using Rok.ViewModels.Album.Services;

namespace Rok.PresentationTests.ViewModels.Album.Services;

public class AlbumEditServiceTests
{
    private readonly FakeMediator _mediator = new();

    private readonly Mock<IDialogService> _dialogService = new();

    private readonly Mock<IAppOptions> _appOptions = new();

    private readonly Mock<IStringResourceProvider> _strings = new();

    private AlbumEditService BuildService()
    {
        _strings.Setup(strings => strings.GetString(It.IsAny<string>())).Returns<string>(key => key);

        return new AlbumEditService(_mediator, _dialogService.Object, _appOptions.Object, _strings.Object, NullLogger<AlbumEditService>.Instance);
    }

    private void SetupEditDialog(AlbumEditValues? returned)
    {
        _dialogService
            .Setup(x => x.ShowEditAlbumAsync(It.IsAny<AlbumEditValues>(), It.IsAny<AlbumTaggedFieldsMode>(), It.IsAny<AlbumEditSuggestions>(), It.IsAny<Func<AlbumEditValues, Task<string?>>>()))
            .ReturnsAsync(returned);
    }

    [Fact(DisplayName = "UpdateFavoriteAsync should send the favorite command and update the album state")]
    public async Task UpdateFavoriteAsync_ShouldSendCommandAndUpdateState()
    {
        // Arrange
        AlbumDto album = new() { Id = 5, IsFavorite = false };
        AlbumEditService sut = BuildService();

        // Act
        await sut.UpdateFavoriteAsync(album, isFavorite: true);

        // Assert
        Assert.True(album.IsFavorite);
        UpdateAlbumFavoriteRequest sent = Assert.Single(_mediator.Sent<UpdateAlbumFavoriteRequest>());
        Assert.Equal(5, sent.Id);
        Assert.True(sent.IsFavorite);
    }

    [Fact(DisplayName = "UpdateTagsAsync should send the tags command for the given album")]
    public async Task UpdateTagsAsync_ShouldSendTagsCommand()
    {
        // Arrange
        AlbumEditService sut = BuildService();
        string[] tags = new[] { "rock", "live" };

        // Act
        await sut.UpdateTagsAsync(id: 5, tags);

        // Assert
        UpdateAlbumTagsRequest sent = Assert.Single(_mediator.Sent<UpdateAlbumTagsRequest>());
        Assert.Equal(5, sent.Id);
        Assert.True(sent.Tags.SequenceEqual(tags));
    }

    [Theory(DisplayName = "UpdatePictureDominantColorAsync should send the color command including null values")]
    [InlineData(0xFF112233L)]
    [InlineData(null)]
    public async Task UpdatePictureDominantColorAsync_ShouldSendCommand(long? colorValue)
    {
        // Arrange
        AlbumEditService sut = BuildService();

        // Act
        await sut.UpdatePictureDominantColorAsync(id: 5, colorValue);

        // Assert
        UpdateAlbumPictureDominantColorRequest sent = Assert.Single(_mediator.Sent<UpdateAlbumPictureDominantColorRequest>());
        Assert.Equal(5, sent.Id);
        Assert.Equal(colorValue, sent.ColorValue);
    }

    [Fact(DisplayName = "EditAlbumAsync confirmed should mutate the album with the edited values")]
    public async Task EditAlbumAsync_Confirmed_ShouldMutateAlbum()
    {
        // Arrange
        AlbumDto album = new()
        {
            Id = 7,
            IsLive = false,
            IsBestOf = false,
            IsCompilation = false,
            IsLock = false,
            Biography = "old bio",
            LastFmUrl = "old-url",
            MusicBrainzID = "old-mb",
            ReleaseGroupMusicBrainzID = "old-rg"
        };

        AlbumEditValues edited = new()
        {
            IsLive = true,
            IsBestOf = true,
            IsCompilation = true,
            IsLock = true,
            Biography = "new bio",
            LastFmUrl = "new-url",
            MusicBrainzID = "new-mb",
            ReleaseGroupMusicBrainzID = "new-rg"
        };

        SetupEditDialog(edited);

        AlbumEditService sut = BuildService();

        // Act
        AlbumEditResult result = await sut.EditAlbumAsync(album);

        // Assert
        Assert.True(result.Updated);
        Assert.Null(result.Outcome);
        Assert.True(album.IsLive);
        Assert.True(album.IsBestOf);
        Assert.True(album.IsCompilation);
        Assert.True(album.IsLock);
        Assert.Equal("new bio", album.Biography);
        Assert.Equal("new-url", album.LastFmUrl);
        Assert.Equal("new-mb", album.MusicBrainzID);
        Assert.Equal("new-rg", album.ReleaseGroupMusicBrainzID);
    }

    [Fact(DisplayName = "EditAlbumAsync confirmed should send an update command with the edited fields")]
    public async Task EditAlbumAsync_Confirmed_ShouldSendUpdateCommand()
    {
        // Arrange
        AlbumDto album = new() { Id = 7 };

        AlbumEditValues edited = new()
        {
            IsLive = true,
            IsBestOf = false,
            IsCompilation = true,
            IsLock = true,
            Biography = "bio",
            LastFmUrl = "url",
            MusicBrainzID = "mb",
            ReleaseGroupMusicBrainzID = "rg"
        };

        SetupEditDialog(edited);

        AlbumEditService sut = BuildService();

        // Act
        await sut.EditAlbumAsync(album);

        // Assert
        UpdateAlbumRequest sent = Assert.Single(_mediator.Sent<UpdateAlbumRequest>());
        Assert.Equal(7, sent.Id);
        Assert.True(sent.IsLive.IsSet);
        Assert.True(sent.IsLive.Value);
        Assert.True(sent.IsBestOf.IsSet);
        Assert.False(sent.IsBestOf.Value);
        Assert.True(sent.IsCompilation.IsSet);
        Assert.True(sent.IsCompilation.Value);
        Assert.True(sent.IsLock.IsSet);
        Assert.True(sent.IsLock.Value);
        Assert.Equal("bio", sent.Biography.Value);
        Assert.Equal("url", sent.LastFmUrl.Value);
        Assert.Equal("mb", sent.MusicBrainzID.Value);
        Assert.Equal("rg", sent.ReleaseGroupMusicBrainzID.Value);
    }

    [Fact(DisplayName = "EditAlbumAsync cancelled should not send any command nor mutate the album")]
    public async Task EditAlbumAsync_Cancelled_ShouldDoNothing()
    {
        // Arrange
        AlbumDto album = new()
        {
            Id = 7,
            IsLive = false,
            Biography = "old bio"
        };

        SetupEditDialog(null);

        AlbumEditService sut = BuildService();

        // Act
        AlbumEditResult result = await sut.EditAlbumAsync(album);

        // Assert
        Assert.False(result.Updated);
        Assert.Empty(_mediator.Sent<UpdateAlbumRequest>());
        Assert.False(album.IsLive);
        Assert.Equal("old bio", album.Biography);
    }
}