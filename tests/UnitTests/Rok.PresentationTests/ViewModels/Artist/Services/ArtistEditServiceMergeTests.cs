using CleanArch.DevKit.Mediator.Results;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Dto;
using Rok.Application.Errors;
using Rok.Application.Features.Artists.Merge;
using Rok.Application.Features.Artists.Rename;
using Rok.Application.Features.Artists.Requests;
using Rok.Application.Interfaces;
using Rok.Services;
using Rok.ViewModels.Artist.Services;

namespace Rok.PresentationTests.ViewModels.Artist.Services;

public class ArtistEditServiceMergeTests
{
    private readonly FakeMediator _mediator = new();
    private readonly Mock<IDialogService> _dialogService = new();
    private readonly Mock<IAppOptions> _appOptions = new();
    private readonly Mock<IStringResourceProvider> _strings = new();

    private ArtistEditService BuildService(bool writeTagsEnabled = true)
    {
        _appOptions.SetupGet(options => options.WriteTagsEnabled).Returns(writeTagsEnabled);
        _strings.Setup(strings => strings.GetString(It.IsAny<string>())).Returns<string>(key => key);

        return new ArtistEditService(_mediator, _dialogService.Object, _appOptions.Object, _strings.Object, NullLogger<ArtistEditService>.Instance);
    }

    private static ArtistMergeOutcome Outcome(EArtistRenameState state, params long[] playlists)
        => new(state, 9, "Target", [], [], playlists, 7, [], []);

    private static ArtistMergePreview Preview()
        => new(7, "Source", 9, "Target", 3, 1, [], [], [], [], EArtistMergePicture.Target);

    private async Task<(ArtistMergeDialogRequest Request, ArtistMergeDialogActions Actions)> OpenMergeAsync(ArtistDto artist, ArtistMergeOutcome? returned = null)
    {
        ArtistMergeDialogRequest? request = null;
        ArtistMergeDialogActions? actions = null;

        _dialogService
            .Setup(x => x.ShowMergeArtistsAsync(It.IsAny<ArtistMergeDialogRequest>(), It.IsAny<ArtistMergeDialogActions>()))
            .Callback<ArtistMergeDialogRequest, ArtistMergeDialogActions>((shown, shownActions) =>
            {
                request = shown;
                actions = shownActions;
            })
            .ReturnsAsync(returned);

        await BuildService().MergeWithAsync(artist);

        return (request!, actions!);
    }

    [Fact(DisplayName = "merge_with_opens_the_dialog_with_the_displayed_artist_kept_by_default")]
    public async Task MergeWithAsync_OpensTheDialogWithTheDisplayedArtist()
    {
        // Arrange
        ArtistDto artist = new() { Id = 7, Name = "Source" };
        ArtistMergeOutcome outcome = Outcome(EArtistRenameState.Completed);

        // Act
        (ArtistMergeDialogRequest request, _) = await OpenMergeAsync(artist, outcome);

        // Assert
        Assert.Equal(new ArtistMergeChoice(7, "Source"), request.First);
        Assert.Null(request.Second);
        Assert.True(request.KeepFirst);
        Assert.Null(request.Intro);
    }

    [Theory(DisplayName = "merge_with_does_nothing_with_writing_tags_off_or_a_pending_rename")]
    [InlineData(false, null)]
    [InlineData(true, "Old")]
    public async Task MergeWithAsync_NotAllowed_ShowsNoDialog(bool writeTagsEnabled, string? pendingFrom)
    {
        // Arrange
        ArtistDto artist = new() { Id = 7, Name = "Source", RenamePendingFrom = pendingFrom };
        ArtistEditService sut = BuildService(writeTagsEnabled);

        // Act
        ArtistMergeOutcome? outcome = await sut.MergeWithAsync(artist);

        // Assert
        Assert.Null(outcome);
        _dialogService.Verify(x => x.ShowMergeArtistsAsync(It.IsAny<ArtistMergeDialogRequest>(), It.IsAny<ArtistMergeDialogActions>()), Times.Never);
    }

    [Fact(DisplayName = "the_merge_preview_is_requested_for_the_merged_and_the_kept_artist")]
    public async Task PreviewAsync_SendsThePreviewRequest()
    {
        // Arrange
        _mediator.Setup<GetArtistMergePreviewRequest, Result<ArtistMergePreview>>().Returns(Result<ArtistMergePreview>.Ok(Preview()));
        (_, ArtistMergeDialogActions actions) = await OpenMergeAsync(new ArtistDto { Id = 7, Name = "Source" });

        // Act
        ArtistMergePreviewAttempt attempt = await actions.PreviewAsync(7, 9);

        // Assert
        Assert.NotNull(attempt.Preview);
        GetArtistMergePreviewRequest sent = Assert.Single(_mediator.Sent<GetArtistMergePreviewRequest>());
        Assert.Equal(7, sent.SourceId);
        Assert.Equal(9, sent.TargetId);
    }

    [Fact(DisplayName = "a_refused_merge_preview_returns_the_translated_message_of_its_code")]
    public async Task PreviewAsync_Refused_ReturnsTheTranslatedMessage()
    {
        // Arrange
        _mediator.Setup<GetArtistMergePreviewRequest, Result<ArtistMergePreview>>().Returns(Result<ArtistMergePreview>.Fail(new ConflictError("artist.merge.pending_rename", "pending")));
        (_, ArtistMergeDialogActions actions) = await OpenMergeAsync(new ArtistDto { Id = 7, Name = "Source" });
        _strings.Setup(strings => strings.GetString("artistMergeError_PendingRename")).Returns("Resume the rename first.");

        // Act
        ArtistMergePreviewAttempt attempt = await actions.PreviewAsync(7, 9);

        // Assert
        Assert.Null(attempt.Preview);
        Assert.Equal("Resume the rename first.", attempt.Error);
    }

    [Fact(DisplayName = "a_merge_runs_the_request_and_reports_its_outcome")]
    public async Task MergeAsync_SendsTheMergeRequest()
    {
        // Arrange
        ArtistMergeOutcome outcome = Outcome(EArtistRenameState.Completed, 4);
        _mediator.Setup<MergeArtistsRequest, Result<ArtistMergeOutcome>>().Returns(Result<ArtistMergeOutcome>.Ok(outcome));
        (_, ArtistMergeDialogActions actions) = await OpenMergeAsync(new ArtistDto { Id = 7, Name = "Source" });

        // Act
        ArtistMergeAttempt attempt = await actions.MergeAsync(7, 9, new Progress<ArtistRenameProgress>());

        // Assert
        Assert.Same(outcome, attempt.Outcome);
        MergeArtistsRequest sent = Assert.Single(_mediator.Sent<MergeArtistsRequest>());
        Assert.Equal(7, sent.SourceId);
        Assert.Equal(9, sent.TargetId);
        Assert.NotNull(sent.Progress);
    }

    [Fact(DisplayName = "a_refused_merge_returns_the_translated_message")]
    public async Task MergeAsync_Refused_ReturnsTheTranslatedMessage()
    {
        // Arrange
        _mediator.Setup<MergeArtistsRequest, Result<ArtistMergeOutcome>>().Returns(Result<ArtistMergeOutcome>.Fail(new ConflictError("artist.merge.same_artist", "same")));
        (_, ArtistMergeDialogActions actions) = await OpenMergeAsync(new ArtistDto { Id = 7, Name = "Source" });
        _strings.Setup(strings => strings.GetString("artistMergeError_SameArtist")).Returns("Choose two different artists.");

        // Act
        ArtistMergeAttempt attempt = await actions.MergeAsync(7, 7, new Progress<ArtistRenameProgress>());

        // Assert
        Assert.Null(attempt.Outcome);
        Assert.Equal("Choose two different artists.", attempt.Error);
    }

    [Fact(DisplayName = "retrying_after_a_partial_merge_resumes_the_rename_of_the_kept_artist_and_keeps_the_merge_ids")]
    public async Task MergeAsync_AfterAPartialMerge_ResumesTheRename()
    {
        // Arrange
        ArtistMergeOutcome partial = new(EArtistRenameState.Partial, 9, "Target", [], [new ArtistRenameFailure("a.mp3", Rok.Application.Tag.ETagWriteReason.Locked)], [4], 7, [21], [22]);
        _mediator.Setup<MergeArtistsRequest, Result<ArtistMergeOutcome>>().Returns(Result<ArtistMergeOutcome>.Ok(partial));
        _mediator.Setup<RenameArtistRequest, Result<ArtistRenameOutcome>>().Returns(Result<ArtistRenameOutcome>.Ok(new ArtistRenameOutcome(EArtistRenameState.Completed, "Target", [], [], [5])));
        (_, ArtistMergeDialogActions actions) = await OpenMergeAsync(new ArtistDto { Id = 7, Name = "Source" });
        await actions.MergeAsync(7, 9, new Progress<ArtistRenameProgress>());

        // Act
        ArtistMergeAttempt retry = await actions.MergeAsync(7, 9, new Progress<ArtistRenameProgress>());

        // Assert
        Assert.Single(_mediator.Sent<MergeArtistsRequest>());
        RenameArtistRequest sent = Assert.Single(_mediator.Sent<RenameArtistRequest>());
        Assert.Equal(9, sent.ArtistId);
        Assert.Equal("Target", sent.NewName);
        Assert.NotNull(sent.Progress);
        Assert.Equal(EArtistRenameState.Completed, retry.Outcome!.State);
        Assert.Empty(retry.Outcome.Failures);
        Assert.Equal(7, retry.Outcome.DeletedArtistId);
        Assert.Equal([21L], retry.Outcome.DeletedAlbumIds);
        Assert.Equal([22L], retry.Outcome.UpdatedAlbumIds);
        Assert.Equal([4L, 5L], retry.Outcome.ChangedPlaylistIds);
    }

    [Fact(DisplayName = "a_blocked_resume_keeps_the_partial_merge_to_resume_again")]
    public async Task MergeAsync_BlockedResume_StaysResumable()
    {
        // Arrange
        ArtistMergeOutcome partial = Outcome(EArtistRenameState.Partial);
        _mediator.Setup<MergeArtistsRequest, Result<ArtistMergeOutcome>>().Returns(Result<ArtistMergeOutcome>.Ok(partial));
        _mediator.Setup<RenameArtistRequest, Result<ArtistRenameOutcome>>().Returns(Result<ArtistRenameOutcome>.Ok(new ArtistRenameOutcome(EArtistRenameState.Blocked, "Target", [new ArtistRenameBlocker("a.mp3", EArtistRenameBlock.Playing, null)], [], [])));
        (_, ArtistMergeDialogActions actions) = await OpenMergeAsync(new ArtistDto { Id = 7, Name = "Source" });
        await actions.MergeAsync(7, 9, new Progress<ArtistRenameProgress>());

        // Act
        ArtistMergeAttempt first = await actions.MergeAsync(7, 9, new Progress<ArtistRenameProgress>());
        ArtistMergeAttempt second = await actions.MergeAsync(7, 9, new Progress<ArtistRenameProgress>());

        // Assert
        Assert.Equal(EArtistRenameState.Blocked, first.Outcome!.State);
        Assert.Equal(EArtistRenameState.Blocked, second.Outcome!.State);
        Assert.Equal(2, _mediator.Sent<RenameArtistRequest>().Count);
        Assert.Single(_mediator.Sent<MergeArtistsRequest>());
    }

    [Fact(DisplayName = "the_artist_search_excludes_the_displayed_artist_and_matches_the_typed_text")]
    public async Task SearchAsync_ExcludesTheDisplayedArtist()
    {
        // Arrange
        _mediator.Setup<GetAllArtistsRequest, IEnumerable<ArtistDto>>().Returns(new List<ArtistDto>
        {
            new() { Id = 7, Name = "AC/DC" },
            new() { Id = 9, Name = "ACDC" },
            new() { Id = 10, Name = "Acid Drinkers" },
            new() { Id = 11, Name = "Metallica" }
        });
        (_, ArtistMergeDialogActions actions) = await OpenMergeAsync(new ArtistDto { Id = 7, Name = "AC/DC" });

        // Act
        IReadOnlyList<ArtistMergeChoice> found = await actions.SearchAsync(" ac");
        IReadOnlyList<ArtistMergeChoice> none = await actions.SearchAsync("zzz");

        // Assert
        Assert.Equal([new ArtistMergeChoice(9, "ACDC"), new ArtistMergeChoice(10, "Acid Drinkers")], found);
        Assert.Empty(none);
        Assert.Single(_mediator.Sent<GetAllArtistsRequest>());
    }
}