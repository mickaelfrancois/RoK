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

public class ArtistEditServiceRenameTests
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

    private static ArtistRenamePreview Preview(string oldName = "Old", string newName = "New", ArtistRenameCollision? collision = null, bool isResume = false)
        => new(7, oldName, newName, 3, 1, [], collision, isResume);

    private static ArtistRenameOutcome Outcome(EArtistRenameState state, params long[] playlists)
        => new(state, "New", [], [], playlists);

    private (Func<string, Task<string?>> Validate, ArtistNameMode Mode) CaptureEditDialog(ArtistEditValues? returned)
    {
        Func<string, Task<string?>>? validate = null;
        ArtistNameMode mode = default;

        _dialogService
            .Setup(x => x.ShowEditArtistAsync(It.IsAny<ArtistEditValues>(), It.IsAny<ArtistNameMode>(), It.IsAny<Func<string, Task<string?>>>()))
            .Callback<ArtistEditValues, ArtistNameMode, Func<string, Task<string?>>>((_, nameMode, validateNameAsync) =>
            {
                mode = nameMode;
                validate = validateNameAsync;
            })
            .ReturnsAsync(returned);

        return (name => validate!(name), mode);
    }

    private void SetupRenameDialog(Func<ArtistRenameAttempt, ArtistRenameOutcome?> map, Action<ArtistRenameAttempt>? onAttempt = null)
    {
        _dialogService
            .Setup(x => x.ShowRenameArtistAsync(It.IsAny<ArtistRenamePreview>(), It.IsAny<Func<IProgress<ArtistRenameProgress>, Task<ArtistRenameAttempt>>>()))
            .Returns<ArtistRenamePreview, Func<IProgress<ArtistRenameProgress>, Task<ArtistRenameAttempt>>>(async (_, run) =>
            {
                ArtistRenameAttempt attempt = await run(new Progress<ArtistRenameProgress>());
                onAttempt?.Invoke(attempt);

                return map(attempt);
            });
    }

    [Fact(DisplayName = "an_unchanged_name_starts_no_rename")]
    public async Task EditArtistAsync_UnchangedName_StartsNoRename()
    {
        // Arrange
        ArtistDto artist = new() { Id = 7, Name = "Old" };
        ArtistEditService sut = BuildService();
        CaptureEditDialog(new ArtistEditValues { Name = "  Old " });

        // Act
        ArtistEditResult result = await sut.EditArtistAsync(artist);

        // Assert
        Assert.True(result.Updated);
        Assert.Null(result.Rename);
        Assert.Empty(_mediator.Sent<GetArtistRenamePreviewRequest>());
        Assert.Empty(_mediator.Sent<RenameArtistRequest>());
        _dialogService.Verify(x => x.ShowRenameArtistAsync(It.IsAny<ArtistRenamePreview>(), It.IsAny<Func<IProgress<ArtistRenameProgress>, Task<ArtistRenameAttempt>>>()), Times.Never);
    }

    [Fact(DisplayName = "the_name_field_is_hidden_when_writing_tags_is_off")]
    public async Task EditArtistAsync_OptionOff_HidesTheNameField()
    {
        // Arrange
        ArtistDto artist = new() { Id = 7, Name = "Old" };
        ArtistEditService sut = BuildService(writeTagsEnabled: false);
        ArtistNameMode? shown = null;

        _dialogService
            .Setup(x => x.ShowEditArtistAsync(It.IsAny<ArtistEditValues>(), It.IsAny<ArtistNameMode>(), It.IsAny<Func<string, Task<string?>>>()))
            .Callback<ArtistEditValues, ArtistNameMode, Func<string, Task<string?>>>((_, mode, _) => shown = mode)
            .ReturnsAsync(new ArtistEditValues { Name = "Other" });

        // Act
        ArtistEditResult result = await sut.EditArtistAsync(artist);

        // Assert
        Assert.Equal(ArtistNameMode.Hidden, shown);
        Assert.Null(result.Rename);
        Assert.Empty(_mediator.Sent<GetArtistRenamePreviewRequest>());
    }

    [Fact(DisplayName = "the_name_field_is_locked_during_a_pending_rename_and_the_name_is_ignored")]
    public async Task EditArtistAsync_PendingRename_LocksTheNameField()
    {
        // Arrange
        ArtistDto artist = new() { Id = 7, Name = "New", RenamePendingFrom = "Old" };
        ArtistEditService sut = BuildService();
        ArtistNameMode? shown = null;

        _dialogService
            .Setup(x => x.ShowEditArtistAsync(It.IsAny<ArtistEditValues>(), It.IsAny<ArtistNameMode>(), It.IsAny<Func<string, Task<string?>>>()))
            .Callback<ArtistEditValues, ArtistNameMode, Func<string, Task<string?>>>((_, mode, _) => shown = mode)
            .ReturnsAsync(new ArtistEditValues { Name = "Yet another" });

        // Act
        ArtistEditResult result = await sut.EditArtistAsync(artist);

        // Assert
        Assert.Equal(ArtistNameMode.Locked, shown);
        Assert.Null(result.Rename);
        Assert.Empty(_mediator.Sent<RenameArtistRequest>());
    }

    [Fact(DisplayName = "a_collision_is_no_longer_refused_in_the_edit_dialog")]
    public async Task ValidateName_Collision_IsAccepted()
    {
        // Arrange
        ArtistDto artist = new() { Id = 7, Name = "Old" };
        _mediator.Setup<GetArtistRenamePreviewRequest, Result<ArtistRenamePreview>>().Returns(Result<ArtistRenamePreview>.Ok(Preview(collision: new ArtistRenameCollision("Taken", 2, 14) { ArtistId = 9 })));
        ArtistEditService sut = BuildService();
        (Func<string, Task<string?>> validate, _) = CaptureEditDialog(null);
        await sut.EditArtistAsync(artist);

        // Act
        string? message = await validate("Taken");

        // Assert
        Assert.Null(message);
        GetArtistRenamePreviewRequest sent = Assert.Single(_mediator.Sent<GetArtistRenamePreviewRequest>());
        Assert.Equal("Taken", sent.NewName);
    }

    [Fact(DisplayName = "rename_collision_offers_a_merge_with_the_existing_artist")]
    public async Task EditArtistAsync_Collision_OffersTheMergeWithTheExistingArtist()
    {
        // Arrange
        ArtistDto artist = new() { Id = 7, Name = "Old", Biography = "old bio" };
        ArtistMergeOutcome outcome = new(EArtistRenameState.Completed, 9, "Taken", [], [], [], 7, [], []);
        _mediator.Setup<GetArtistRenamePreviewRequest, Result<ArtistRenamePreview>>().Returns(Result<ArtistRenamePreview>.Ok(Preview(newName: "Taken", collision: new ArtistRenameCollision("Taken", 2, 14) { ArtistId = 9 })));
        ArtistEditService sut = BuildService();
        _strings.Setup(strings => strings.GetString(ArtistMergeTextKeys.Collision)).Returns("{0} exists ({1} albums, {2} tracks). Merge?");
        Func<string, Task<string?>>? validate = null;
        string? validation = "not validated";

        _dialogService
            .Setup(x => x.ShowEditArtistAsync(It.IsAny<ArtistEditValues>(), It.IsAny<ArtistNameMode>(), It.IsAny<Func<string, Task<string?>>>()))
            .Callback<ArtistEditValues, ArtistNameMode, Func<string, Task<string?>>>((_, _, validateNameAsync) => validate = validateNameAsync)
            .Returns(async () =>
            {
                validation = await validate!("Taken");

                return new ArtistEditValues { Name = "Taken", Biography = "new bio" };
            });

        ArtistMergeDialogRequest? shown = null;
        _dialogService
            .Setup(x => x.ShowMergeArtistsAsync(It.IsAny<ArtistMergeDialogRequest>(), It.IsAny<ArtistMergeDialogActions>()))
            .Callback<ArtistMergeDialogRequest, ArtistMergeDialogActions>((request, _) => shown = request)
            .ReturnsAsync(outcome);

        // Act
        ArtistEditResult result = await sut.EditArtistAsync(artist);

        // Assert
        Assert.Null(validation);
        Assert.True(result.Updated);
        Assert.Same(outcome, result.Merge);
        Assert.True(result.MergeApplied);
        Assert.Null(result.Rename);
        Assert.Equal("new bio", Assert.Single(_mediator.Sent<UpdateArtistRequest>()).Biography.Value);
        Assert.NotNull(shown);
        Assert.Equal(new ArtistMergeChoice(7, "Old"), shown.First);
        Assert.Equal(new ArtistMergeChoice(9, "Taken"), shown.Second);
        Assert.False(shown.KeepFirst);
        Assert.Equal("Taken exists (2 albums, 14 tracks). Merge?", shown.Intro);
        Assert.Empty(_mediator.Sent<RenameArtistRequest>());
        _dialogService.Verify(x => x.ShowRenameArtistAsync(It.IsAny<ArtistRenamePreview>(), It.IsAny<Func<IProgress<ArtistRenameProgress>, Task<ArtistRenameAttempt>>>()), Times.Never);
        _dialogService.Verify(x => x.ShowTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>()), Times.Never);
    }

    [Theory(DisplayName = "a_blank_or_too_long_name_is_refused_without_a_request")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ValidateName_BlankOrTooLong_ReturnsTheInvalidNameMessage(string typed)
    {
        // Arrange
        ArtistDto artist = new() { Id = 7, Name = "Old" };
        ArtistEditService sut = BuildService();
        (Func<string, Task<string?>> validate, _) = CaptureEditDialog(null);
        await sut.EditArtistAsync(artist);

        // Act
        string? blank = await validate(typed);
        string? tooLong = await validate(new string('a', ArtistRenameLimits.MaxNameLength + 1));

        // Assert
        Assert.Equal(ArtistRenameTextKeys.InvalidName, blank);
        Assert.Equal(ArtistRenameTextKeys.InvalidName, tooLong);
        Assert.Empty(_mediator.Sent<GetArtistRenamePreviewRequest>());
    }

    [Fact(DisplayName = "the_current_name_is_accepted_without_a_request")]
    public async Task ValidateName_CurrentName_IsAccepted()
    {
        // Arrange
        ArtistDto artist = new() { Id = 7, Name = "Old" };
        ArtistEditService sut = BuildService();
        (Func<string, Task<string?>> validate, _) = CaptureEditDialog(null);
        await sut.EditArtistAsync(artist);

        // Act
        string? message = await validate("Old");

        // Assert
        Assert.Null(message);
        Assert.Empty(_mediator.Sent<GetArtistRenamePreviewRequest>());
    }

    [Fact(DisplayName = "a_refused_preview_returns_the_translated_message_of_its_code")]
    public async Task ValidateName_RefusedPreview_ReturnsTheTranslatedMessage()
    {
        // Arrange
        ArtistDto artist = new() { Id = 7, Name = "Old" };
        _mediator.Setup<GetArtistRenamePreviewRequest, Result<ArtistRenamePreview>>().Returns(Result<ArtistRenamePreview>.Fail(new OperationError("artist.rename.disabled", "off")));
        ArtistEditService sut = BuildService();
        _strings.Setup(strings => strings.GetString("trackEditError_Disabled")).Returns("Writing tags is turned off.");
        (Func<string, Task<string?>> validate, _) = CaptureEditDialog(null);
        await sut.EditArtistAsync(artist);

        // Act
        string? message = await validate("New");

        // Assert
        Assert.Equal("Writing tags is turned off.", message);
    }

    [Fact(DisplayName = "a_new_name_renames_after_the_fields_are_saved_and_reuses_the_checked_preview")]
    public async Task EditArtistAsync_NewName_RenamesAfterSavingTheFields()
    {
        // Arrange
        ArtistDto artist = new() { Id = 7, Name = "Old", Biography = "old bio" };
        ArtistRenameOutcome outcome = Outcome(EArtistRenameState.Completed, 3, 4);
        _mediator.Setup<GetArtistRenamePreviewRequest, Result<ArtistRenamePreview>>().Returns(Result<ArtistRenamePreview>.Ok(Preview()));
        _mediator.Setup<RenameArtistRequest, Result<ArtistRenameOutcome>>().Returns(Result<ArtistRenameOutcome>.Ok(outcome));
        ArtistEditService sut = BuildService();
        Func<string, Task<string?>>? validate = null;

        _dialogService
            .Setup(x => x.ShowEditArtistAsync(It.IsAny<ArtistEditValues>(), It.IsAny<ArtistNameMode>(), It.IsAny<Func<string, Task<string?>>>()))
            .Callback<ArtistEditValues, ArtistNameMode, Func<string, Task<string?>>>((_, _, validateNameAsync) => validate = validateNameAsync)
            .Returns(async () =>
            {
                await validate!("New");

                return new ArtistEditValues { Name = "New", Biography = "new bio" };
            });

        SetupRenameDialog(attempt => attempt.Outcome);

        // Act
        ArtistEditResult result = await sut.EditArtistAsync(artist);

        // Assert
        Assert.True(result.Updated);
        Assert.Same(outcome, result.Rename);
        Assert.True(result.NameChanged);
        Assert.Equal("new bio", Assert.Single(_mediator.Sent<UpdateArtistRequest>()).Biography.Value);
        Assert.Single(_mediator.Sent<GetArtistRenamePreviewRequest>());
        RenameArtistRequest sent = Assert.Single(_mediator.Sent<RenameArtistRequest>());
        Assert.Equal(7, sent.ArtistId);
        Assert.Equal("New", sent.NewName);
        Assert.NotNull(sent.Progress);
    }

    [Fact(DisplayName = "a_name_not_checked_in_the_dialog_loads_its_preview_before_the_rename")]
    public async Task EditArtistAsync_NameNotChecked_LoadsThePreview()
    {
        // Arrange
        ArtistDto artist = new() { Id = 7, Name = "Old" };
        _mediator.Setup<GetArtistRenamePreviewRequest, Result<ArtistRenamePreview>>().Returns(Result<ArtistRenamePreview>.Ok(Preview()));
        _mediator.Setup<RenameArtistRequest, Result<ArtistRenameOutcome>>().Returns(Result<ArtistRenameOutcome>.Ok(Outcome(EArtistRenameState.Completed)));
        ArtistEditService sut = BuildService();
        CaptureEditDialog(new ArtistEditValues { Name = " New " });
        SetupRenameDialog(attempt => attempt.Outcome);

        // Act
        ArtistEditResult result = await sut.EditArtistAsync(artist);

        // Assert
        Assert.True(result.NameChanged);
        Assert.Equal("New", Assert.Single(_mediator.Sent<GetArtistRenamePreviewRequest>()).NewName);
        Assert.Equal("New", Assert.Single(_mediator.Sent<RenameArtistRequest>()).NewName);
    }

    [Fact(DisplayName = "a_collision_found_after_the_dialog_offers_the_merge_and_starts_no_rename")]
    public async Task EditArtistAsync_CollisionAfterTheDialog_OffersTheMerge()
    {
        // Arrange
        ArtistDto artist = new() { Id = 7, Name = "Old" };
        _mediator.Setup<GetArtistRenamePreviewRequest, Result<ArtistRenamePreview>>().Returns(Result<ArtistRenamePreview>.Ok(Preview(collision: new ArtistRenameCollision("New", 1, 2) { ArtistId = 9 })));
        ArtistEditService sut = BuildService();
        CaptureEditDialog(new ArtistEditValues { Name = "New" });
        _dialogService
            .Setup(x => x.ShowMergeArtistsAsync(It.IsAny<ArtistMergeDialogRequest>(), It.IsAny<ArtistMergeDialogActions>()))
            .ReturnsAsync((ArtistMergeOutcome?)null);

        // Act
        ArtistEditResult result = await sut.EditArtistAsync(artist);

        // Assert
        Assert.True(result.Updated);
        Assert.Null(result.Rename);
        Assert.False(result.MergeApplied);
        Assert.Empty(_mediator.Sent<RenameArtistRequest>());
        _dialogService.Verify(x => x.ShowMergeArtistsAsync(It.Is<ArtistMergeDialogRequest>(request => request.Second!.Id == 9), It.IsAny<ArtistMergeDialogActions>()), Times.Once);
    }

    [Fact(DisplayName = "a_collision_without_the_existing_artist_id_offers_nothing")]
    public async Task EditArtistAsync_CollisionWithoutId_OffersNothing()
    {
        // Arrange
        ArtistDto artist = new() { Id = 7, Name = "Old" };
        _mediator.Setup<GetArtistRenamePreviewRequest, Result<ArtistRenamePreview>>().Returns(Result<ArtistRenamePreview>.Ok(Preview(collision: new ArtistRenameCollision("New", 1, 2))));
        ArtistEditService sut = BuildService();
        CaptureEditDialog(new ArtistEditValues { Name = "New" });

        // Act
        ArtistEditResult result = await sut.EditArtistAsync(artist);

        // Assert
        Assert.True(result.Updated);
        Assert.Null(result.Merge);
        _dialogService.Verify(x => x.ShowMergeArtistsAsync(It.IsAny<ArtistMergeDialogRequest>(), It.IsAny<ArtistMergeDialogActions>()), Times.Never);
    }

    [Fact(DisplayName = "a_refused_rename_returns_the_translated_message_to_the_dialog")]
    public async Task EditArtistAsync_RefusedRename_ReturnsTheMessageToTheDialog()
    {
        // Arrange
        ArtistDto artist = new() { Id = 7, Name = "Old" };
        _mediator.Setup<GetArtistRenamePreviewRequest, Result<ArtistRenamePreview>>().Returns(Result<ArtistRenamePreview>.Ok(Preview()));
        _mediator.Setup<RenameArtistRequest, Result<ArtistRenameOutcome>>().Returns(Result<ArtistRenameOutcome>.Fail(new ConflictError("artist.rename.collision", "taken")));
        ArtistEditService sut = BuildService();
        _strings.Setup(strings => strings.GetString("artistRenameError_Collision")).Returns("Another artist already has this name.");
        CaptureEditDialog(new ArtistEditValues { Name = "New" });
        ArtistRenameAttempt? seen = null;
        SetupRenameDialog(_ => null, attempt => seen = attempt);

        // Act
        ArtistEditResult result = await sut.EditArtistAsync(artist);

        // Assert
        Assert.NotNull(seen);
        Assert.Null(seen.Outcome);
        Assert.Equal("Another artist already has this name.", seen.Error);
        Assert.False(result.NameChanged);
    }

    [Fact(DisplayName = "a_partial_rename_still_counts_as_a_changed_name")]
    public async Task EditArtistAsync_PartialRename_IsANameChange()
    {
        // Arrange
        ArtistDto artist = new() { Id = 7, Name = "Old" };
        _mediator.Setup<GetArtistRenamePreviewRequest, Result<ArtistRenamePreview>>().Returns(Result<ArtistRenamePreview>.Ok(Preview()));
        _mediator.Setup<RenameArtistRequest, Result<ArtistRenameOutcome>>().Returns(Result<ArtistRenameOutcome>.Ok(Outcome(EArtistRenameState.Partial)));
        ArtistEditService sut = BuildService();
        CaptureEditDialog(new ArtistEditValues { Name = "New" });
        SetupRenameDialog(attempt => attempt.Outcome);

        // Act
        ArtistEditResult result = await sut.EditArtistAsync(artist);

        // Assert
        Assert.True(result.NameChanged);
        Assert.Equal(EArtistRenameState.Partial, result.Rename!.State);
    }

    [Fact(DisplayName = "a_blocked_rename_is_not_a_name_change")]
    public async Task EditArtistAsync_BlockedRename_IsNotANameChange()
    {
        // Arrange
        ArtistDto artist = new() { Id = 7, Name = "Old" };
        _mediator.Setup<GetArtistRenamePreviewRequest, Result<ArtistRenamePreview>>().Returns(Result<ArtistRenamePreview>.Ok(Preview()));
        _mediator.Setup<RenameArtistRequest, Result<ArtistRenameOutcome>>().Returns(Result<ArtistRenameOutcome>.Ok(Outcome(EArtistRenameState.Blocked)));
        ArtistEditService sut = BuildService();
        CaptureEditDialog(new ArtistEditValues { Name = "New" });
        SetupRenameDialog(attempt => attempt.Outcome);

        // Act
        ArtistEditResult result = await sut.EditArtistAsync(artist);

        // Assert
        Assert.False(result.NameChanged);
    }

    [Fact(DisplayName = "resume_previews_the_current_name_and_renames_to_it")]
    public async Task ResumeRenameAsync_PendingRename_RenamesToTheCurrentName()
    {
        // Arrange
        ArtistDto artist = new() { Id = 7, Name = "New", RenamePendingFrom = "Old" };
        ArtistRenameOutcome outcome = Outcome(EArtistRenameState.Completed);
        _mediator.Setup<GetArtistRenamePreviewRequest, Result<ArtistRenamePreview>>().Returns(Result<ArtistRenamePreview>.Ok(Preview(isResume: true)));
        _mediator.Setup<RenameArtistRequest, Result<ArtistRenameOutcome>>().Returns(Result<ArtistRenameOutcome>.Ok(outcome));
        ArtistEditService sut = BuildService();
        SetupRenameDialog(attempt => attempt.Outcome);

        // Act
        ArtistEditResult result = await sut.ResumeRenameAsync(artist);

        // Assert
        Assert.False(result.Updated);
        Assert.Same(outcome, result.Rename);
        Assert.Equal("New", Assert.Single(_mediator.Sent<GetArtistRenamePreviewRequest>()).NewName);
        Assert.Equal("New", Assert.Single(_mediator.Sent<RenameArtistRequest>()).NewName);
    }

    [Theory(DisplayName = "resume_does_nothing_without_a_pending_rename_or_with_writing_tags_off")]
    [InlineData(true, null)]
    [InlineData(true, "")]
    [InlineData(false, "Old")]
    public async Task ResumeRenameAsync_NothingToResume_DoesNothing(bool writeTagsEnabled, string? pendingFrom)
    {
        // Arrange
        ArtistDto artist = new() { Id = 7, Name = "New", RenamePendingFrom = pendingFrom };
        ArtistEditService sut = BuildService(writeTagsEnabled);

        // Act
        ArtistEditResult result = await sut.ResumeRenameAsync(artist);

        // Assert
        Assert.Same(ArtistEditResult.Nothing, result);
        Assert.Empty(_mediator.Sent<GetArtistRenamePreviewRequest>());
        Assert.Empty(_mediator.Sent<RenameArtistRequest>());
    }
}