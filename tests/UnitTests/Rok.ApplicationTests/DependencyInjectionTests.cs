using Microsoft.Extensions.DependencyInjection;
using Rok.Application;
using Rok.Application.Features.Albums.Edit;
using Rok.Application.Features.Albums.Merge;
using Rok.Application.Features.Albums.Requests;
using Rok.Application.Features.Artists.Merge;
using Rok.Application.Features.Artists.Rename;
using Rok.Application.Features.Artists.Requests;
using Rok.Application.Features.Tags;
using Rok.Application.Player.Mix;

namespace Rok.ApplicationTests;

public class DependencyInjectionTests
{
    [Fact(DisplayName = "mix_cue_provider_is_registered")]
    public void AddApplication_RegistersMixCueProvider()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddApplication();

        // Assert
        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(IMixCueProvider));
        Assert.Equal(typeof(MixAnalysisService), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact(DisplayName = "artist_rename_service_is_registered")]
    public void AddApplication_RegistersArtistRenameService()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddApplication();

        // Assert
        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(ArtistRenameService));
        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
    }

    [Fact(DisplayName = "artist_file_rewriter_is_registered")]
    public void AddApplication_RegistersArtistFileRewriter()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddApplication();

        // Assert
        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(ArtistFileRewriter));
        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
    }

    [Fact(DisplayName = "track_file_tag_writer_is_registered")]
    public void AddApplication_RegistersTrackFileTagWriter()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddApplication();

        // Assert
        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(TrackFileTagWriter));
        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
    }

    [Fact(DisplayName = "artist_rename_handlers_are_registered")]
    public void AddApplication_RegistersArtistRenameHandlers()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddApplication();

        // Assert
        Assert.Contains(services, d => d.ServiceType == typeof(IRequestHandler<RenameArtistRequest, Result<ArtistRenameOutcome>>));
        Assert.Contains(services, d => d.ServiceType == typeof(IRequestHandler<GetArtistRenamePreviewRequest, Result<ArtistRenamePreview>>));
    }

    [Fact(DisplayName = "artist_merge_service_is_registered")]
    public void AddApplication_RegistersArtistMergeService()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddApplication();

        // Assert
        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(ArtistMergeService));
        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
    }

    [Fact(DisplayName = "artist_merge_handlers_are_registered")]
    public void AddApplication_RegistersArtistMergeHandlers()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddApplication();

        // Assert
        Assert.Contains(services, d => d.ServiceType == typeof(IRequestHandler<MergeArtistsRequest, Result<ArtistMergeOutcome>>));
        Assert.Contains(services, d => d.ServiceType == typeof(IRequestHandler<GetArtistMergePreviewRequest, Result<ArtistMergePreview>>));
    }

    [Fact(DisplayName = "album_edit_service_is_registered")]
    public void AddApplication_RegistersAlbumEditService()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddApplication();

        // Assert
        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(AlbumEditService));
        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
    }

    [Fact(DisplayName = "album_edit_handlers_are_registered")]
    public void AddApplication_RegistersAlbumEditHandlers()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddApplication();

        // Assert
        Assert.Contains(services, d => d.ServiceType == typeof(IRequestHandler<GetAlbumEditPreviewRequest, Result<AlbumEditPreview>>));
        Assert.Contains(services, d => d.ServiceType == typeof(IRequestHandler<EditAlbumTagsRequest, Result<AlbumTagWriteOutcome>>));
        Assert.Contains(services, d => d.ServiceType == typeof(IRequestHandler<ResumeAlbumTagWriteRequest, Result<AlbumTagWriteOutcome>>));
    }

    [Fact(DisplayName = "album_merge_service_is_registered")]
    public void AddApplication_RegistersAlbumMergeService()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddApplication();

        // Assert
        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(AlbumMergeService));
        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
    }

    [Fact(DisplayName = "album_merge_handlers_are_registered")]
    public void AddApplication_RegistersAlbumMergeHandlers()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddApplication();

        // Assert
        Assert.Contains(services, d => d.ServiceType == typeof(IRequestHandler<GetAlbumMergePreviewRequest, Result<AlbumMergePreview>>));
        Assert.Contains(services, d => d.ServiceType == typeof(IRequestHandler<MergeAlbumsRequest, Result<AlbumTagWriteOutcome>>));
    }
}