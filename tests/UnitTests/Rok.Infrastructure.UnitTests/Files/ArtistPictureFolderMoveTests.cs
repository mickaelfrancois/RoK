using Moq;
using Rok.Application.Interfaces;
using Rok.Application.Interfaces.Pictures;
using Rok.Infrastructure.Files;

namespace Rok.Infrastructure.UnitTests.Files;

public sealed class ArtistPictureFolderMoveTests : IDisposable
{
    private readonly string _cachePath = Path.Combine(Path.GetTempPath(), "rok-artist-move-" + Guid.NewGuid().ToString("N"));
    private readonly ArtistPicture _sut;

    public ArtistPictureFolderMoveTests()
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
        Directory.CreateDirectory(Folder(folder));
        File.WriteAllText(Path.Combine(Folder(folder), fileName), content);
    }

    [Fact(DisplayName = "check_returns_nothing_to_do_when_the_source_folder_is_missing")]
    public void Check_NoSourceFolder_NothingToDo()
    {
        // Act
        ArtistFolderMoveOutcome outcome = _sut.CheckFolderMove("Old", "New", []);

        // Assert
        Assert.Equal(ArtistFolderMoveOutcome.NothingToDo, outcome);
    }

    [Fact(DisplayName = "check_returns_nothing_to_do_when_the_folder_name_does_not_change")]
    public void Check_SameFolderName_NothingToDo()
    {
        // Arrange
        WriteFile("Old", "artist.jpg", "a");

        // Act
        ArtistFolderMoveOutcome outcome = _sut.CheckFolderMove("Old", "Old", []);

        // Assert
        Assert.Equal(ArtistFolderMoveOutcome.NothingToDo, outcome);
    }

    [Fact(DisplayName = "check_is_blocked_when_the_new_name_gives_an_empty_folder_name")]
    public void Check_EmptyFolderName_Blocked()
    {
        // Arrange
        WriteFile("Old", "artist.jpg", "a");

        // Act
        ArtistFolderMoveOutcome outcome = _sut.CheckFolderMove("Old", "...", []);

        // Assert
        Assert.Equal(ArtistFolderMoveOutcome.Blocked, outcome);
    }

    [Fact(DisplayName = "move_never_touches_the_artists_root_when_the_new_folder_name_is_empty")]
    public void Move_EmptyFolderName_LeavesRootIntact()
    {
        // Arrange
        WriteFile("Old", "artist.jpg", "a");

        // Act
        ArtistFolderMoveOutcome outcome = _sut.MoveFolder("Old", "...");

        // Assert
        Assert.Equal(ArtistFolderMoveOutcome.Blocked, outcome);
        Assert.True(File.Exists(Path.Combine(Folder("Old"), "artist.jpg")));
        Assert.True(Directory.Exists(_sut.RepositoryArtistPath));
    }

    [Fact(DisplayName = "move_renames_the_folder_with_its_picture_and_backdrops")]
    public void Move_FreeTarget_MovesAllFiles()
    {
        // Arrange
        WriteFile("Old", "artist.jpg", "picture");
        WriteFile("Old", "backdrop1.jpg", "backdrop");

        // Act
        ArtistFolderMoveOutcome outcome = _sut.MoveFolder("Old", "New");

        // Assert
        Assert.Equal(ArtistFolderMoveOutcome.Move, outcome);
        Assert.False(Directory.Exists(Folder("Old")));
        Assert.Equal("picture", File.ReadAllText(Path.Combine(Folder("New"), "artist.jpg")));
        Assert.Equal("backdrop", File.ReadAllText(Path.Combine(Folder("New"), "backdrop1.jpg")));
    }

    [Fact(DisplayName = "move_replaces_an_empty_target_folder")]
    public void Move_EmptyTarget_DeletesThenMoves()
    {
        // Arrange
        WriteFile("Old", "artist.jpg", "picture");
        Directory.CreateDirectory(Folder("New"));

        // Act
        ArtistFolderMoveOutcome check = _sut.CheckFolderMove("Old", "New", []);
        ArtistFolderMoveOutcome outcome = _sut.MoveFolder("Old", "New");

        // Assert
        Assert.Equal(ArtistFolderMoveOutcome.ReplaceEmptyTarget, check);
        Assert.Equal(ArtistFolderMoveOutcome.ReplaceEmptyTarget, outcome);
        Assert.False(Directory.Exists(Folder("Old")));
        Assert.Equal("picture", File.ReadAllText(Path.Combine(Folder("New"), "artist.jpg")));
    }

    [Fact(DisplayName = "move_merges_into_an_orphan_target_and_the_renamed_artist_images_win")]
    public void Move_OrphanTarget_MergesRenamedArtistWins()
    {
        // Arrange
        WriteFile("Old", "artist.jpg", "renamed-picture");
        WriteFile("Old", "backdrop1.jpg", "renamed-backdrop");
        WriteFile("New", "artist.jpg", "orphan-picture");
        WriteFile("New", "backdrop2.jpg", "orphan-backdrop");

        // Act
        ArtistFolderMoveOutcome check = _sut.CheckFolderMove("Old", "New", ["Someone Else"]);
        ArtistFolderMoveOutcome outcome = _sut.MoveFolder("Old", "New");

        // Assert
        Assert.Equal(ArtistFolderMoveOutcome.Merge, check);
        Assert.Equal(ArtistFolderMoveOutcome.Merge, outcome);
        Assert.False(Directory.Exists(Folder("Old")));
        Assert.Equal("renamed-picture", File.ReadAllText(Path.Combine(Folder("New"), "artist.jpg")));
        Assert.Equal("renamed-backdrop", File.ReadAllText(Path.Combine(Folder("New"), "backdrop1.jpg")));
        Assert.Equal("orphan-backdrop", File.ReadAllText(Path.Combine(Folder("New"), "backdrop2.jpg")));
    }

    [Fact(DisplayName = "check_is_blocked_when_the_target_folder_belongs_to_another_living_artist")]
    public void Check_TargetOwnedByAnotherArtist_Blocked()
    {
        // Arrange
        WriteFile("Old", "artist.jpg", "renamed-picture");
        WriteFile("New", "artist.jpg", "other-picture");

        // Act
        ArtistFolderMoveOutcome outcome = _sut.CheckFolderMove("Old", "New", ["new"]);

        // Assert
        Assert.Equal(ArtistFolderMoveOutcome.Blocked, outcome);
    }

    [Fact(DisplayName = "check_leaves_both_folders_untouched")]
    public void Check_Blocked_LeavesFoldersUntouched()
    {
        // Arrange
        WriteFile("Old", "artist.jpg", "renamed-picture");
        WriteFile("New", "artist.jpg", "other-picture");

        // Act
        ArtistFolderMoveOutcome check = _sut.CheckFolderMove("Old", "New", ["New"]);

        // Assert
        Assert.Equal(ArtistFolderMoveOutcome.Blocked, check);
        Assert.Equal("renamed-picture", File.ReadAllText(Path.Combine(Folder("Old"), "artist.jpg")));
        Assert.Equal("other-picture", File.ReadAllText(Path.Combine(Folder("New"), "artist.jpg")));
    }

    [Fact(DisplayName = "move_renames_a_case_only_change_in_two_steps")]
    public void Move_CaseOnlyChange_RenamesFolder()
    {
        // Arrange
        WriteFile("elodie", "artist.jpg", "picture");

        // Act
        ArtistFolderMoveOutcome check = _sut.CheckFolderMove("elodie", "Elodie", []);
        ArtistFolderMoveOutcome outcome = _sut.MoveFolder("elodie", "Elodie");

        // Assert
        Assert.Equal(ArtistFolderMoveOutcome.Move, check);
        Assert.Equal(ArtistFolderMoveOutcome.Move, outcome);

        string[] names = Directory.GetDirectories(_sut.RepositoryArtistPath).Select(Path.GetFileName).OfType<string>().ToArray();

        Assert.Equal(["Elodie"], names);
        Assert.Equal("picture", File.ReadAllText(Path.Combine(Folder("Elodie"), "artist.jpg")));
    }

    [Fact(DisplayName = "move_is_a_no_op_when_the_source_folder_is_missing")]
    public void Move_NoSourceFolder_NoOp()
    {
        // Act
        ArtistFolderMoveOutcome outcome = _sut.MoveFolder("Old", "New");

        // Assert
        Assert.Equal(ArtistFolderMoveOutcome.NothingToDo, outcome);
        Assert.False(Directory.Exists(Folder("New")));
    }
}