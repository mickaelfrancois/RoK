using CleanArch.DevKit.Mediator.Results;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Dto;
using Rok.Application.Errors;
using Rok.Application.Features.Albums.Merge;
using Rok.Application.Features.Albums.Requests;
using Rok.Application.Features.Artists.Rename;
using Rok.Application.Interfaces;
using Rok.Services;
using Rok.ViewModels.Album.Services;
using AlbumCollision = Rok.Application.Features.Albums.Edit.AlbumCollision;
using AlbumEditPreview = Rok.Application.Features.Albums.Edit.AlbumEditPreview;
using AlbumTagWriteOutcome = Rok.Application.Features.Albums.Edit.AlbumTagWriteOutcome;

namespace Rok.PresentationTests.ViewModels.Album.Services;

public class AlbumEditServiceMergeTests
{
    private readonly FakeMediator _mediator = new();
    private readonly Mock<IDialogService> _dialogService = new();
    private readonly Mock<IAppOptions> _appOptions = new();
    private readonly Mock<IStringResourceProvider> _strings = new();

    private AlbumEditService BuildService(bool writeTagsEnabled = true)
    {
        _appOptions.SetupGet(options => options.WriteTagsEnabled).Returns(writeTagsEnabled);
        _strings.Setup(strings => strings.GetString(It.IsAny<string>())).Returns<string>(key => key);

        return new AlbumEditService(_mediator, _dialogService.Object, _appOptions.Object, _strings.Object, NullLogger<AlbumEditService>.Instance);
    }

    private static AlbumDto Album(bool isCompilation = false) => new()
    {
        Id = 7,
        Name = "Abbey Road",
        ArtistId = 3,
        ArtistName = "The Beatles",
        Year = 1969,
        GenreName = "Rock",
        IsCompilation = isCompilation
    };

    private static AlbumEditPreview CollisionPreview(AlbumCollision collision) => new(
        7, "Abbey Road", "Abbey Road (Remaster)", "The Beatles", "The Beatles", false, 1969, 1969, "Rock", "Rock", false, 3, 0, 0, 0, collision, null, []);

    private static AlbumMergePreview MergePreview() => new(7, "Abbey Road", 9, "Abbey Road (Remaster)", "The Beatles", "The Beatles", false, 3, 17, 3, [], [], 0, false, null, []);

    private static AlbumTagWriteOutcome Outcome(EArtistRenameState state, long albumId = 9, long[]? deleted = null, long[]? artists = null) => new(state, albumId, [], [], artists ?? [3], deleted ?? [7]);

    private (Func<AlbumMergeDialogRequest?> Request, Func<AlbumMergeDialogActions?> Actions) CaptureMergeDialog(AlbumTagWriteOutcome? returned = null)
    {
        AlbumMergeDialogRequest? request = null;
        AlbumMergeDialogActions? actions = null;

        _dialogService
            .Setup(x => x.ShowMergeAlbumsAsync(It.IsAny<AlbumMergeDialogRequest>(), It.IsAny<AlbumMergeDialogActions>()))
            .Callback<AlbumMergeDialogRequest, AlbumMergeDialogActions>((shown, shownActions) =>
            {
                request = shown;
                actions = shownActions;
            })
            .ReturnsAsync(returned);

        return (() => request, () => actions);
    }

    private void SetupEditDialog(string newName)
    {
        _dialogService
            .Setup(x => x.ShowEditAlbumAsync(It.IsAny<AlbumEditValues>(), It.IsAny<AlbumTaggedFieldsMode>(), It.IsAny<AlbumEditSuggestions>(), It.IsAny<Func<AlbumEditValues, Task<string?>>>()))
            .ReturnsAsync(new AlbumEditValues { Name = newName, ArtistName = "The Beatles", Year = "1969", GenreName = "Rock" });
    }

    [Fact(DisplayName = "a_collision_opens_the_merge_dialog_keeping_the_existing_album")]
    public async Task EditAlbumAsync_Collision_OpensTheMergeDialogWithTheExistingAlbumAsTarget()
    {
        // Arrange
        AlbumEditService sut = BuildService();
        AlbumCollision collision = new(9, "Abbey Road (Remaster)", "The Beatles", 17);
        _mediator.Setup<GetAlbumEditPreviewRequest, Result<AlbumEditPreview>>().Returns(Result<AlbumEditPreview>.Ok(CollisionPreview(collision)));
        SetupEditDialog("Abbey Road (Remaster)");
        (Func<AlbumMergeDialogRequest?> request, Func<AlbumMergeDialogActions?> _) = CaptureMergeDialog(Outcome(EArtistRenameState.Completed));

        // Act
        AlbumEditResult result = await sut.EditAlbumAsync(Album());

        // Assert
        AlbumMergeDialogRequest shown = Assert.IsType<AlbumMergeDialogRequest>(request());
        Assert.Equal(new AlbumMergeChoice(7, "Abbey Road", "The Beatles"), shown.First);
        Assert.Equal(new AlbumMergeChoice(9, "Abbey Road (Remaster)", "The Beatles"), shown.Second);
        Assert.False(shown.KeepFirst);
        Assert.Equal("albumMergeCollision", shown.Intro);
        Assert.True(result.Updated);
        Assert.Equal(EArtistRenameState.Completed, result.Outcome?.State);
        Assert.True(result.TagsChanged);
        _dialogService.Verify(x => x.ShowTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>()), Times.Never);
        _dialogService.Verify(x => x.ShowAlbumTagWriteAsync(It.IsAny<AlbumTagWritePrompt>(), It.IsAny<Func<IProgress<ArtistRenameProgress>, Task<AlbumTagWriteAttempt>>>()), Times.Never);
        Assert.Empty(_mediator.Sent<EditAlbumTagsRequest>());
    }

    [Fact(DisplayName = "a_collision_with_an_unknown_album_is_refused_with_a_message")]
    public async Task EditAlbumAsync_CollisionWithoutAnId_ShowsTheRefusal()
    {
        // Arrange
        AlbumEditService sut = BuildService();
        AlbumCollision collision = new(0, "Abbey Road (Remaster)", "The Beatles", 17);
        _mediator.Setup<GetAlbumEditPreviewRequest, Result<AlbumEditPreview>>().Returns(Result<AlbumEditPreview>.Ok(CollisionPreview(collision)));
        SetupEditDialog("Abbey Road (Remaster)");
        CaptureMergeDialog();

        // Act
        AlbumEditResult result = await sut.EditAlbumAsync(Album());

        // Assert
        Assert.Null(result.Outcome);
        _dialogService.Verify(x => x.ShowTextAsync("Abbey Road", "albumEditCollision", false, "fr"), Times.Once);
        _dialogService.Verify(x => x.ShowMergeAlbumsAsync(It.IsAny<AlbumMergeDialogRequest>(), It.IsAny<AlbumMergeDialogActions>()), Times.Never);
    }

    [Fact(DisplayName = "merge_with_opens_the_dialog_keeping_the_displayed_album")]
    public async Task MergeWithAsync_OpensTheDialogWithTheDisplayedAlbumAsTarget()
    {
        // Arrange
        AlbumEditService sut = BuildService();
        (Func<AlbumMergeDialogRequest?> request, Func<AlbumMergeDialogActions?> _) = CaptureMergeDialog(Outcome(EArtistRenameState.Completed));

        // Act
        AlbumTagWriteOutcome? outcome = await sut.MergeWithAsync(Album());

        // Assert
        AlbumMergeDialogRequest shown = Assert.IsType<AlbumMergeDialogRequest>(request());
        Assert.Equal(new AlbumMergeChoice(7, "Abbey Road", "The Beatles"), shown.First);
        Assert.Null(shown.Second);
        Assert.True(shown.KeepFirst);
        Assert.Null(shown.Intro);
        Assert.Equal(EArtistRenameState.Completed, outcome?.State);
    }

    [Theory(DisplayName = "merge_with_does_nothing_when_writing_tags_is_off_or_the_album_is_a_compilation")]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task MergeWithAsync_NotAvailable_OpensNothing(bool writeTagsEnabled, bool isCompilation)
    {
        // Arrange
        AlbumEditService sut = BuildService(writeTagsEnabled);

        // Act
        AlbumTagWriteOutcome? outcome = await sut.MergeWithAsync(Album(isCompilation));

        // Assert
        Assert.Null(outcome);
        _dialogService.Verify(x => x.ShowMergeAlbumsAsync(It.IsAny<AlbumMergeDialogRequest>(), It.IsAny<AlbumMergeDialogActions>()), Times.Never);
    }

    [Fact(DisplayName = "the_dialog_preview_action_returns_the_preview_or_the_translated_refusal")]
    public async Task PreviewAction_ReturnsThePreviewOrTheRefusal()
    {
        // Arrange
        AlbumEditService sut = BuildService();
        (Func<AlbumMergeDialogRequest?> _, Func<AlbumMergeDialogActions?> actions) = CaptureMergeDialog();
        await sut.MergeWithAsync(Album());
        _mediator.Setup<GetAlbumMergePreviewRequest, Result<AlbumMergePreview>>().Returns(Result<AlbumMergePreview>.Ok(MergePreview()));

        // Act
        AlbumMergePreviewAttempt success = await actions()!.PreviewAsync(7, 9);
        _mediator.Setup<GetAlbumMergePreviewRequest, Result<AlbumMergePreview>>().Returns(Result<AlbumMergePreview>.Fail(new OperationError("album.merge.compilation", "no")));
        AlbumMergePreviewAttempt refused = await actions()!.PreviewAsync(7, 9);

        // Assert
        Assert.NotNull(success.Preview);
        Assert.Null(refused.Preview);
        Assert.Equal("albumMergeError_Compilation", refused.Error);
        Assert.Equal(7, _mediator.Sent<GetAlbumMergePreviewRequest>()[0].SourceId);
        Assert.Equal(9, _mediator.Sent<GetAlbumMergePreviewRequest>()[0].TargetId);
    }

    [Fact(DisplayName = "the_dialog_merge_action_sends_the_albums_and_the_disc_choice_and_maps_a_refusal")]
    public async Task MergeAction_SendsTheRequestAndMapsTheRefusal()
    {
        // Arrange
        AlbumEditService sut = BuildService();
        (Func<AlbumMergeDialogRequest?> _, Func<AlbumMergeDialogActions?> actions) = CaptureMergeDialog();
        await sut.MergeWithAsync(Album());
        _mediator.Setup<MergeAlbumsRequest, Result<AlbumTagWriteOutcome>>().Returns(Result<AlbumTagWriteOutcome>.Ok(Outcome(EArtistRenameState.Completed)));

        // Act
        AlbumTagWriteAttempt done = await actions()!.MergeAsync(7, 9, true, new Progress<ArtistRenameProgress>());
        _mediator.Setup<MergeAlbumsRequest, Result<AlbumTagWriteOutcome>>().Returns(Result<AlbumTagWriteOutcome>.Fail(new ConflictError("album.merge.conflicts", "no")));
        AlbumTagWriteAttempt refused = await actions()!.MergeAsync(7, 9, false, new Progress<ArtistRenameProgress>());

        // Assert
        Assert.Equal(EArtistRenameState.Completed, done.Outcome?.State);
        Assert.Null(refused.Outcome);
        Assert.Equal("albumMergeError_Conflicts", refused.Error);
        MergeAlbumsRequest first = _mediator.Sent<MergeAlbumsRequest>()[0];
        Assert.Equal((7L, 9L, true), (first.SourceId, first.TargetId, first.ShiftDiscs));
        Assert.False(_mediator.Sent<MergeAlbumsRequest>()[1].ShiftDiscs);
    }

    [Fact(DisplayName = "a_retry_after_a_partial_merge_resumes_the_target_and_keeps_the_deleted_albums")]
    public async Task MergeAction_AfterAPartialOutcome_ResumesTheTarget()
    {
        // Arrange
        AlbumEditService sut = BuildService();
        (Func<AlbumMergeDialogRequest?> _, Func<AlbumMergeDialogActions?> actions) = CaptureMergeDialog();
        await sut.MergeWithAsync(Album());
        _mediator.Setup<MergeAlbumsRequest, Result<AlbumTagWriteOutcome>>().Returns(Result<AlbumTagWriteOutcome>.Ok(Outcome(EArtistRenameState.Partial, 9, [7], [3])));
        _mediator.Setup<ResumeAlbumTagWriteRequest, Result<AlbumTagWriteOutcome>>().Returns(Result<AlbumTagWriteOutcome>.Ok(Outcome(EArtistRenameState.Completed, 9, [], [4])));

        // Act
        AlbumTagWriteAttempt partial = await actions()!.MergeAsync(7, 9, false, new Progress<ArtistRenameProgress>());
        AlbumTagWriteAttempt resumed = await actions()!.MergeAsync(7, 9, false, new Progress<ArtistRenameProgress>());

        // Assert
        Assert.Equal(EArtistRenameState.Partial, partial.Outcome?.State);
        Assert.Equal(EArtistRenameState.Completed, resumed.Outcome?.State);
        Assert.Equal(9, resumed.Outcome?.AlbumId);
        Assert.Equal([7L], resumed.Outcome?.DeletedAlbumIds);
        Assert.Equal([3L, 4L], resumed.Outcome?.UpdatedArtistIds);
        Assert.Single(_mediator.Sent<MergeAlbumsRequest>());
        Assert.Equal(9, Assert.Single(_mediator.Sent<ResumeAlbumTagWriteRequest>()).AlbumId);
    }

    [Fact(DisplayName = "the_dialog_search_action_skips_the_displayed_album_and_the_compilations")]
    public async Task SearchAction_SkipsTheDisplayedAlbumAndTheCompilations()
    {
        // Arrange
        AlbumEditService sut = BuildService();
        (Func<AlbumMergeDialogRequest?> _, Func<AlbumMergeDialogActions?> actions) = CaptureMergeDialog();
        await sut.MergeWithAsync(Album());
        _mediator.Setup<GetAllAlbumsRequest, IEnumerable<AlbumDto>>().Returns(new List<AlbumDto>
        {
            new() { Id = 7, Name = "Abbey Road", ArtistName = "The Beatles" },
            new() { Id = 9, Name = "Abbey Road (Remaster)", ArtistName = "The Beatles" },
            new() { Id = 11, Name = "Abbey Road Sessions", ArtistName = "Various", IsCompilation = true },
            new() { Id = 12, Name = "Revolver", ArtistName = "The Beatles" }
        });

        // Act
        IReadOnlyList<AlbumMergeChoice> found = await actions()!.SearchAsync("abbey");

        // Assert
        Assert.Equal([9L], found.Select(choice => choice.Id));
        Assert.Equal("Abbey Road (Remaster) — The Beatles", found[0].Label);
    }
}