using Rok.Import.Services;

namespace Rok.ImportTests.Services;

public class AlbumFolderKeyTests
{
    [Theory(DisplayName = "album_folder_key_maps_disc_subfolder_to_parent")]
    [InlineData("CD1")]
    [InlineData("CD 2")]
    [InlineData("Disc 2")]
    [InlineData("Disk1")]
    [InlineData("Disque 3")]
    [InlineData("cd01")]
    [InlineData("DISC_2")]
    public void Of_DiscSubfolder_ShouldReturnParent(string discFolder)
    {
        // Arrange
        string directory = Path.Combine(@"C:\music", "Hits", discFolder);

        // Act
        string result = AlbumFolderKey.Of(directory);

        // Assert
        Assert.Equal(Path.Combine(@"C:\music", "Hits"), result);
    }

    [Theory(DisplayName = "album_folder_key_keeps_regular_folder")]
    [InlineData("CD Projekt")]
    [InlineData("Discography")]
    [InlineData("Disco 2000")]
    [InlineData("Disc 2000 Hits")]
    [InlineData("CD1 - Bonus")]
    [InlineData("Hits")]
    public void Of_RegularFolder_ShouldReturnItUnchanged(string folder)
    {
        // Arrange
        string directory = Path.Combine(@"C:\music", folder);

        // Act
        string result = AlbumFolderKey.Of(directory);

        // Assert
        Assert.Equal(directory, result);
    }

    [Fact(DisplayName = "album_folder_key_ignores_trailing_separators")]
    public void Of_TrailingSeparator_ShouldBeRemoved()
    {
        // Arrange
        string directory = @"C:\music\Hits\CD2\";

        // Act
        string result = AlbumFolderKey.Of(directory);

        // Assert
        Assert.Equal(@"C:\music\Hits", result);
    }

    [Fact(DisplayName = "album_folder_key_of_null_is_empty")]
    public void Of_Null_ShouldReturnEmpty()
    {
        // Arrange & Act
        string result = AlbumFolderKey.Of(null);

        // Assert
        Assert.Equal(string.Empty, result);
    }
}