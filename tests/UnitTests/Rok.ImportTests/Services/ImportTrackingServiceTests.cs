using Rok.Import.Services;

namespace Rok.ImportTests.Services;

public class ImportTrackingServiceTests
{
    [Fact(DisplayName = "TrackRead should record the track id as tracked")]
    public void TrackRead_ShouldRecordTrackIdAsTracked()
    {
        // Arrange
        ImportTrackingService service = new();

        // Act
        service.TrackRead(42);

        // Assert
        Assert.Equal(new[] { 42L }, service.GetTrackedIds());
    }

    [Fact(DisplayName = "TrackRead should deduplicate repeated ids")]
    public void TrackRead_ShouldDeduplicate_RepeatedIds()
    {
        // Arrange
        ImportTrackingService service = new();

        // Act
        service.TrackRead(1);
        service.TrackRead(1);
        service.TrackRead(2);

        // Assert
        Assert.Equal(2, service.GetTrackedIds().Count());
    }

    [Fact(DisplayName = "ArtistUpdated should ignore null ids")]
    public void ArtistUpdated_ShouldIgnoreNullIds()
    {
        // Arrange
        ImportTrackingService service = new();

        // Act
        service.ArtistUpdated(null);

        // Assert
        Assert.Empty(service.GetUpdatedArtists());
    }

    [Fact(DisplayName = "GenreUpdated and AlbumUpdated should record ids independently")]
    public void GenreUpdated_AndAlbumUpdated_ShouldRecordIdsIndependently()
    {
        // Arrange
        ImportTrackingService service = new();

        // Act
        service.GenreUpdated(10);
        service.AlbumUpdated(20);
        service.AlbumUpdated(21);

        // Assert
        Assert.Equal(new[] { 10L }, service.GetUpdatedGenres());
        Assert.Equal(new[] { 20L, 21L }, service.GetUpdatedAlbums());
    }

    [Fact(DisplayName = "GetUpdated accessors should return empty enumeration when nothing was tracked")]
    public void GetUpdatedAccessors_ShouldReturnEmpty_WhenNothingTracked()
    {
        // Arrange
        ImportTrackingService service = new();

        // Act & Assert
        Assert.Empty(service.GetTrackedIds());
        Assert.Empty(service.GetUpdatedArtists());
        Assert.Empty(service.GetUpdatedGenres());
        Assert.Empty(service.GetUpdatedAlbums());
    }

    [Fact(DisplayName = "Clear should remove every tracked id across categories")]
    public void Clear_ShouldRemoveEveryTrackedId_AcrossCategories()
    {
        // Arrange
        ImportTrackingService service = new();
        service.TrackRead(1);
        service.ArtistUpdated(2);
        service.GenreUpdated(3);
        service.AlbumUpdated(4);

        // Act
        service.Clear();

        // Assert
        Assert.Empty(service.GetTrackedIds());
        Assert.Empty(service.GetUpdatedArtists());
        Assert.Empty(service.GetUpdatedGenres());
        Assert.Empty(service.GetUpdatedAlbums());
    }
}
