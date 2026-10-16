using Moq;
using Rok.Application.Interfaces;
using Rok.Application.Interfaces.Pictures;
using Rok.Infrastructure.Files;

namespace Rok.Infrastructure.UnitTests.Files;

public sealed class ArtistPictureFolderMergeTests : IDisposable
{
    private readonly string _cachePath = Path.Combine(Path.GetTempPath(), "rok-artist-merge-" + Guid.NewGuid().ToString("N"));
    private readonly ArtistPicture _sut;

    public ArtistPictureFolderMergeTests()
    {
        Directory.CreateDirectory(_cachePath);

        Mock<IAppOptions> options = new();
        options.Setup(o => o.CachePath).Returns(_cachePath);

        _sut = new ArtistPicture(Mock.Of<IFileSystem>(), options.Object);
        Directory.CreateDirectory(_sut.RepositoryArtistPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_cachePath))
            Directory.Delete(_cachePath, true);
    }

    private string Folder(string name) => Path.Combine(_sut.RepositoryArtistPath, name);

    private void WriteFile(string folder, string fileName, string content)
    {
        string path = Path.Combine(Folder(folder), fileName);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    [Fact(DisplayName = "folder_merge_does_nothing_when_the_source_folder_is_missing")]
    public void Merge_SourceMissing_NothingToDo()
    {
        // Arrange
        WriteFile("Target", "artist.jpg", "t");

        // Act
        ArtistFolderMergeOutcome outcome = _sut.MergeFolderInto("Source", "Target", []);

        // Assert
        Assert.Equal(ArtistFolderMergeOutcome.NothingToDo, outcome);
        Assert.Equal("t", File.ReadAllText(Path.Combine(Folder("Target"), "artist.jpg")));
    }

    [Fact(DisplayName = "folder_merge_does_nothing_when_both_names_share_the_same_folder")]
    public void Merge_SameFolder_NothingToDo()
    {
        // Arrange
        WriteFile("ACDC", "artist.jpg", "a");

        // Act
        ArtistFolderMergeOutcome outcome = _sut.MergeFolderInto("AC/DC", "ACDC", []);

        // Assert
        Assert.Equal(ArtistFolderMergeOutcome.NothingToDo, outcome);
        Assert.True(File.Exists(Path.Combine(Folder("ACDC"), "artist.jpg")));
    }

    [Fact(DisplayName = "folder_merge_moves_the_whole_folder_when_the_target_is_missing")]
    public void Merge_TargetMissing_Moved()
    {
        // Arrange
        WriteFile("Source", "artist.jpg", "s");

        // Act
        ArtistFolderMergeOutcome outcome = _sut.MergeFolderInto("Source", "Target", []);

        // Assert
        Assert.Equal(ArtistFolderMergeOutcome.Moved, outcome);
        Assert.False(Directory.Exists(Folder("Source")));
        Assert.Equal("s", File.ReadAllText(Path.Combine(Folder("Target"), "artist.jpg")));
    }

    [Fact(DisplayName = "folder_merge_moves_the_whole_folder_when_the_target_is_empty")]
    public void Merge_TargetEmpty_Moved()
    {
        // Arrange
        WriteFile("Source", "artist.jpg", "s");
        Directory.CreateDirectory(Folder("Target"));

        // Act
        ArtistFolderMergeOutcome outcome = _sut.MergeFolderInto("Source", "Target", []);

        // Assert
        Assert.Equal(ArtistFolderMergeOutcome.Moved, outcome);
        Assert.False(Directory.Exists(Folder("Source")));
        Assert.Equal("s", File.ReadAllText(Path.Combine(Folder("Target"), "artist.jpg")));
    }

    [Fact(DisplayName = "folder_merge_keeps_the_target_files_and_moves_the_missing_ones")]
    public void Merge_TargetFull_TargetWins()
    {
        // Arrange
        WriteFile("Source", "artist.jpg", "source-picture");
        WriteFile("Source", "backdrop1.jpg", "source-backdrop");
        WriteFile(Path.Combine("Source", "sub"), "deep.jpg", "source-deep");
        WriteFile("Target", "artist.jpg", "target-picture");

        // Act
        ArtistFolderMergeOutcome outcome = _sut.MergeFolderInto("Source", "Target", []);

        // Assert
        Assert.Equal(ArtistFolderMergeOutcome.Merged, outcome);
        Assert.False(Directory.Exists(Folder("Source")));
        Assert.Equal("target-picture", File.ReadAllText(Path.Combine(Folder("Target"), "artist.jpg")));
        Assert.Equal("source-backdrop", File.ReadAllText(Path.Combine(Folder("Target"), "backdrop1.jpg")));
        Assert.Equal("source-deep", File.ReadAllText(Path.Combine(Folder("Target"), "sub", "deep.jpg")));
    }

    [Fact(DisplayName = "folder_merge_leaves_a_source_folder_shared_with_another_living_artist")]
    public void Merge_SourceSharedWithAnotherArtist_NothingToDo()
    {
        // Arrange
        WriteFile("ACDC", "artist.jpg", "s");
        WriteFile("Target", "artist.jpg", "t");

        // Act
        ArtistFolderMergeOutcome outcome = _sut.MergeFolderInto("ACDC", "Target", ["AC/DC"]);

        // Assert
        Assert.Equal(ArtistFolderMergeOutcome.NothingToDo, outcome);
        Assert.Equal("s", File.ReadAllText(Path.Combine(Folder("ACDC"), "artist.jpg")));
        Assert.Equal("t", File.ReadAllText(Path.Combine(Folder("Target"), "artist.jpg")));
    }

    [Theory(DisplayName = "folder_merge_never_touches_the_root_when_a_folder_name_is_empty")]
    [InlineData("...", "Target")]
    [InlineData("Source", "...")]
    public void Merge_EmptyFolderName_NothingToDo(string sourceName, string targetName)
    {
        // Arrange
        WriteFile("Source", "artist.jpg", "s");
        WriteFile("Target", "artist.jpg", "t");

        // Act
        ArtistFolderMergeOutcome outcome = _sut.MergeFolderInto(sourceName, targetName, []);

        // Assert
        Assert.Equal(ArtistFolderMergeOutcome.NothingToDo, outcome);
        Assert.True(File.Exists(Path.Combine(Folder("Source"), "artist.jpg")));
        Assert.True(File.Exists(Path.Combine(Folder("Target"), "artist.jpg")));
        Assert.True(Directory.Exists(_sut.RepositoryArtistPath));
    }
}