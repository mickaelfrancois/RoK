using CleanArch.DevKit.Mediator.Results;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Dto;
using Rok.Application.Errors;
using Rok.Application.Features.Albums.Requests;
using Rok.Application.Features.Artists.Rename;
using Rok.Application.Features.Artists.Requests;
using Rok.Application.Interfaces;
using Rok.Services;
using Rok.ViewModels.Album.Services;
using AlbumCollision = Rok.Application.Features.Albums.Edit.AlbumCollision;
using AlbumEditPreview = Rok.Application.Features.Albums.Edit.AlbumEditPreview;
using AlbumTagWriteOutcome = Rok.Application.Features.Albums.Edit.AlbumTagWriteOutcome;

namespace Rok.PresentationTests.ViewModels.Album.Services;

public class AlbumEditServiceTaggedFieldsTests
{
    private readonly FakeMediator _mediator = new();
    private readonly Mock<IDialogService> _dialogService = new();
    private readonly Mock<IAppOptions> _appOptions = new();
    private readonly Mock<IStringResourceProvider> _strings = new();

    private AlbumEditService BuildService(bool writeTagsEnabled = true, bool genreFromArtist = false)
    {
        _appOptions.SetupGet(options => options.WriteTagsEnabled).Returns(writeTagsEnabled);
        _appOptions.SetupGet(options => options.ImportTrackWithArtistGenre).Returns(genreFromArtist);
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

    private static AlbumEditValues Values(string name = "Abbey Road", string artist = "The Beatles", string year = "1969", string genre = "Rock", bool isCompilation = false) => new()
    {
        Name = name,
        ArtistName = artist,
        Year = year,
        GenreName = genre,
        IsCompilation = isCompilation
    };

    private static AlbumEditPreview Preview(AlbumCollision? collision = null) => new(
        7, "Abbey Road", "Abbey Road (Remaster)", "The Beatles", "The Beatles", false, 1969, 1969, "Rock", "Rock", false, 3, 0, 0, 0, collision, null, []);

    private static AlbumTagWriteOutcome Outcome(EArtistRenameState state) => new(state, 7, [], [], [3], []);

    private Func<AlbumEditValues, Task<string?>> CaptureEditDialog(AlbumEditValues? returned)
    {
        Func<AlbumEditValues, Task<string?>>? validate = null;

        _dialogService
            .Setup(x => x.ShowEditAlbumAsync(It.IsAny<AlbumEditValues>(), It.IsAny<AlbumTaggedFieldsMode>(), It.IsAny<AlbumEditSuggestions>(), It.IsAny<Func<AlbumEditValues, Task<string?>>>()))
            .Callback<AlbumEditValues, AlbumTaggedFieldsMode, AlbumEditSuggestions, Func<AlbumEditValues, Task<string?>>>((_, _, _, validateAsync) => validate = validateAsync)
            .ReturnsAsync(returned);

        return values => validate!(values);
    }

    private void SetupWriteDialog(Func<AlbumTagWriteAttempt, AlbumTagWriteOutcome?> map, Action<AlbumTagWriteAttempt>? onAttempt = null)
    {
        _dialogService
            .Setup(x => x.ShowAlbumTagWriteAsync(It.IsAny<AlbumTagWritePrompt>(), It.IsAny<Func<IProgress<ArtistRenameProgress>, Task<AlbumTagWriteAttempt>>>()))
            .Returns<AlbumTagWritePrompt, Func<IProgress<ArtistRenameProgress>, Task<AlbumTagWriteAttempt>>>(async (_, run) =>
            {
                AlbumTagWriteAttempt attempt = await run(new Progress<ArtistRenameProgress>());
                onAttempt?.Invoke(attempt);

                return map(attempt);
            });
    }

    private void VerifyNoWriteDialog()
    {
        _dialogService.Verify(x => x.ShowAlbumTagWriteAsync(It.IsAny<AlbumTagWritePrompt>(), It.IsAny<Func<IProgress<ArtistRenameProgress>, Task<AlbumTagWriteAttempt>>>()), Times.Never);
    }

    [Fact(DisplayName = "nothing_is_sent_when_no_tagged_field_changes")]
    public async Task EditAlbumAsync_NoTaggedChange_SendsNothing()
    {
        // Arrange
        AlbumEditService sut = BuildService();
        CaptureEditDialog(Values(name: "  Abbey Road ", year: " 1969 "));

        // Act
        AlbumEditResult result = await sut.EditAlbumAsync(Album());

        // Assert
        Assert.True(result.Updated);
        Assert.Null(result.Outcome);
        Assert.Single(_mediator.Sent<UpdateAlbumRequest>());
        Assert.Empty(_mediator.Sent<GetAlbumEditPreviewRequest>());
        Assert.Empty(_mediator.Sent<EditAlbumTagsRequest>());
        VerifyNoWriteDialog();
    }

    [Fact(DisplayName = "the_tagged_fields_are_hidden_and_ignored_when_writing_tags_is_off")]
    public async Task EditAlbumAsync_OptionOff_HidesTheTaggedFields()
    {
        // Arrange
        AlbumEditService sut = BuildService(writeTagsEnabled: false);
        AlbumTaggedFieldsMode? shown = null;

        _dialogService
            .Setup(x => x.ShowEditAlbumAsync(It.IsAny<AlbumEditValues>(), It.IsAny<AlbumTaggedFieldsMode>(), It.IsAny<AlbumEditSuggestions>(), It.IsAny<Func<AlbumEditValues, Task<string?>>>()))
            .Callback<AlbumEditValues, AlbumTaggedFieldsMode, AlbumEditSuggestions, Func<AlbumEditValues, Task<string?>>>((_, mode, _, _) => shown = mode)
            .ReturnsAsync(Values(name: "Other", artist: "Someone", year: "2000", genre: "Jazz"));

        // Act
        AlbumEditResult result = await sut.EditAlbumAsync(Album());

        // Assert
        Assert.Equal(AlbumTaggedFieldsMode.Hidden, shown);
        Assert.True(result.Updated);
        Assert.Null(result.Outcome);
        Assert.Empty(_mediator.Sent<GetAllArtistsRequest>());
        Assert.Empty(_mediator.Sent<GetAlbumEditPreviewRequest>());
        Assert.Empty(_mediator.Sent<EditAlbumTagsRequest>());
        VerifyNoWriteDialog();
    }

    [Fact(DisplayName = "the_tagged_fields_are_hidden_while_a_write_is_pending")]
    public async Task EditAlbumAsync_PendingWrite_HidesTheTaggedFields()
    {
        // Arrange
        _mediator.Setup<GetAlbumPendingTagWriteRequest, Result<bool>>().Returns(Result<bool>.Ok(true));
        AlbumEditService sut = BuildService();
        AlbumTaggedFieldsMode? shown = null;

        _dialogService
            .Setup(x => x.ShowEditAlbumAsync(It.IsAny<AlbumEditValues>(), It.IsAny<AlbumTaggedFieldsMode>(), It.IsAny<AlbumEditSuggestions>(), It.IsAny<Func<AlbumEditValues, Task<string?>>>()))
            .Callback<AlbumEditValues, AlbumTaggedFieldsMode, AlbumEditSuggestions, Func<AlbumEditValues, Task<string?>>>((_, mode, _, _) => shown = mode)
            .ReturnsAsync(Values(name: "Other"));

        // Act
        AlbumEditResult result = await sut.EditAlbumAsync(Album());

        // Assert
        Assert.Equal(AlbumTaggedFieldsMode.Hidden, shown);
        Assert.Null(result.Outcome);
        Assert.Empty(_mediator.Sent<GetAlbumEditPreviewRequest>());
    }

    [Fact(DisplayName = "the_artist_is_locked_for_a_compilation_and_the_typed_artist_is_ignored")]
    public async Task EditAlbumAsync_Compilation_LocksTheArtist()
    {
        // Arrange
        AlbumEditService sut = BuildService();
        AlbumTaggedFieldsMode? shown = null;

        _dialogService
            .Setup(x => x.ShowEditAlbumAsync(It.IsAny<AlbumEditValues>(), It.IsAny<AlbumTaggedFieldsMode>(), It.IsAny<AlbumEditSuggestions>(), It.IsAny<Func<AlbumEditValues, Task<string?>>>()))
            .Callback<AlbumEditValues, AlbumTaggedFieldsMode, AlbumEditSuggestions, Func<AlbumEditValues, Task<string?>>>((_, mode, _, _) => shown = mode)
            .ReturnsAsync(Values(name: "Abbey Road (Remaster)", artist: "Someone else"));
        _mediator.Setup<GetAlbumEditPreviewRequest, Result<AlbumEditPreview>>().Returns(Result<AlbumEditPreview>.Ok(Preview()));
        SetupWriteDialog(_ => null);

        // Act
        await sut.EditAlbumAsync(Album(isCompilation: true));

        // Assert
        Assert.Equal(AlbumTaggedFieldsMode.ArtistLocked, shown);
        GetAlbumEditPreviewRequest sent = Assert.Single(_mediator.Sent<GetAlbumEditPreviewRequest>());
        Assert.Equal("The Beatles", sent.ArtistName);
        Assert.Equal("Abbey Road (Remaster)", sent.Name);
    }

    [Fact(DisplayName = "a_collision_on_a_compilation_is_refused_with_a_message_and_nothing_is_written")]
    public async Task EditAlbumAsync_CollisionOnACompilation_ShowsTheRefusal()
    {
        // Arrange
        AlbumEditService sut = BuildService();
        AlbumCollision collision = new(9, "Abbey Road (Remaster)", "The Beatles", 17);
        _mediator.Setup<GetAlbumEditPreviewRequest, Result<AlbumEditPreview>>().Returns(Result<AlbumEditPreview>.Ok(Preview(collision)));
        CaptureEditDialog(Values(name: "Abbey Road (Remaster)", isCompilation: true));

        // Act
        AlbumEditResult result = await sut.EditAlbumAsync(Album(isCompilation: true));

        // Assert
        Assert.True(result.Updated);
        Assert.Null(result.Outcome);
        _dialogService.Verify(x => x.ShowTextAsync("Abbey Road", "albumEditCollision", false, "fr"), Times.Once);
        Assert.Empty(_mediator.Sent<EditAlbumTagsRequest>());
        VerifyNoWriteDialog();
    }

    [Fact(DisplayName = "a_changed_name_opens_the_write_dialog_and_returns_its_outcome")]
    public async Task EditAlbumAsync_ChangedName_ReturnsTheWriteOutcome()
    {
        // Arrange
        AlbumEditService sut = BuildService();
        AlbumTagWritePrompt? prompt = null;
        _mediator.Setup<GetAlbumEditPreviewRequest, Result<AlbumEditPreview>>().Returns(Result<AlbumEditPreview>.Ok(Preview()));
        _mediator.Setup<EditAlbumTagsRequest, Result<AlbumTagWriteOutcome>>().Returns(Result<AlbumTagWriteOutcome>.Ok(Outcome(EArtistRenameState.Completed)));
        CaptureEditDialog(Values(name: "  Abbey Road (Remaster) ", year: "1970", genre: ""));
        _dialogService
            .Setup(x => x.ShowAlbumTagWriteAsync(It.IsAny<AlbumTagWritePrompt>(), It.IsAny<Func<IProgress<ArtistRenameProgress>, Task<AlbumTagWriteAttempt>>>()))
            .Returns<AlbumTagWritePrompt, Func<IProgress<ArtistRenameProgress>, Task<AlbumTagWriteAttempt>>>(async (shown, run) =>
            {
                prompt = shown;
                AlbumTagWriteAttempt attempt = await run(new Progress<ArtistRenameProgress>());

                return attempt.Outcome;
            });

        // Act
        AlbumEditResult result = await sut.EditAlbumAsync(Album());

        // Assert
        Assert.True(result.TagsChanged);
        Assert.NotNull(prompt);
        Assert.False(prompt.IsResume);
        EditAlbumTagsRequest sent = Assert.Single(_mediator.Sent<EditAlbumTagsRequest>());
        Assert.Equal(7, sent.AlbumId);
        Assert.Equal("Abbey Road (Remaster)", sent.Name);
        Assert.Equal("The Beatles", sent.ArtistName);
        Assert.Equal(1970, sent.Year);
        Assert.Null(sent.GenreName);
    }

    [Fact(DisplayName = "the_write_dialog_retry_resumes_the_write_after_a_partial_outcome")]
    public async Task EditAlbumAsync_RetryAfterPartial_ResumesTheWrite()
    {
        // Arrange
        AlbumEditService sut = BuildService();
        _mediator.Setup<GetAlbumEditPreviewRequest, Result<AlbumEditPreview>>().Returns(Result<AlbumEditPreview>.Ok(Preview()));
        _mediator.Setup<EditAlbumTagsRequest, Result<AlbumTagWriteOutcome>>().Returns(Result<AlbumTagWriteOutcome>.Ok(Outcome(EArtistRenameState.Partial)));
        _mediator.Setup<ResumeAlbumTagWriteRequest, Result<AlbumTagWriteOutcome>>().Returns(Result<AlbumTagWriteOutcome>.Ok(Outcome(EArtistRenameState.Completed)));
        CaptureEditDialog(Values(name: "Abbey Road (Remaster)"));
        _dialogService
            .Setup(x => x.ShowAlbumTagWriteAsync(It.IsAny<AlbumTagWritePrompt>(), It.IsAny<Func<IProgress<ArtistRenameProgress>, Task<AlbumTagWriteAttempt>>>()))
            .Returns<AlbumTagWritePrompt, Func<IProgress<ArtistRenameProgress>, Task<AlbumTagWriteAttempt>>>(async (_, run) =>
            {
                await run(new Progress<ArtistRenameProgress>());
                AlbumTagWriteAttempt second = await run(new Progress<ArtistRenameProgress>());

                return second.Outcome;
            });

        // Act
        AlbumEditResult result = await sut.EditAlbumAsync(Album());

        // Assert
        Assert.Equal(EArtistRenameState.Completed, result.Outcome?.State);
        Assert.Single(_mediator.Sent<EditAlbumTagsRequest>());
        Assert.Single(_mediator.Sent<ResumeAlbumTagWriteRequest>());
    }

    [Fact(DisplayName = "the_retry_after_a_partial_edit_keeps_the_old_artist_to_refresh")]
    public async Task EditAlbumAsync_RetryAfterPartialWithArtistChange_KeepsTheOldArtist()
    {
        // Arrange
        AlbumEditService sut = BuildService();
        _mediator.Setup<GetAlbumEditPreviewRequest, Result<AlbumEditPreview>>().Returns(Result<AlbumEditPreview>.Ok(Preview()));
        _mediator.Setup<EditAlbumTagsRequest, Result<AlbumTagWriteOutcome>>().Returns(Result<AlbumTagWriteOutcome>.Ok(new AlbumTagWriteOutcome(EArtistRenameState.Partial, 7, [], [], [3, 4], [])));
        _mediator.Setup<ResumeAlbumTagWriteRequest, Result<AlbumTagWriteOutcome>>().Returns(Result<AlbumTagWriteOutcome>.Ok(new AlbumTagWriteOutcome(EArtistRenameState.Completed, 7, [], [], [4], [])));
        CaptureEditDialog(Values(artist: "Paul McCartney"));
        _dialogService
            .Setup(x => x.ShowAlbumTagWriteAsync(It.IsAny<AlbumTagWritePrompt>(), It.IsAny<Func<IProgress<ArtistRenameProgress>, Task<AlbumTagWriteAttempt>>>()))
            .Returns<AlbumTagWritePrompt, Func<IProgress<ArtistRenameProgress>, Task<AlbumTagWriteAttempt>>>(async (_, run) =>
            {
                await run(new Progress<ArtistRenameProgress>());
                AlbumTagWriteAttempt second = await run(new Progress<ArtistRenameProgress>());

                return second.Outcome;
            });

        // Act
        AlbumEditResult result = await sut.EditAlbumAsync(Album());

        // Assert
        Assert.Equal(EArtistRenameState.Completed, result.Outcome?.State);
        Assert.Equal([3L, 4L], result.Outcome!.UpdatedArtistIds.Order());
    }

    [Theory(DisplayName = "the_genre_lock_follows_the_artist_genre_import_option")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task EditAlbumAsync_GenreFromArtistOption_DrivesTheGenreLock(bool genreFromArtist)
    {
        // Arrange
        AlbumEditService sut = BuildService(genreFromArtist: genreFromArtist);
        AlbumEditValues? shown = null;

        _dialogService
            .Setup(x => x.ShowEditAlbumAsync(It.IsAny<AlbumEditValues>(), It.IsAny<AlbumTaggedFieldsMode>(), It.IsAny<AlbumEditSuggestions>(), It.IsAny<Func<AlbumEditValues, Task<string?>>>()))
            .Callback<AlbumEditValues, AlbumTaggedFieldsMode, AlbumEditSuggestions, Func<AlbumEditValues, Task<string?>>>((current, _, _, _) => shown = current)
            .ReturnsAsync(Values(name: "Abbey Road (Remaster)", genre: "Jazz"));
        _mediator.Setup<GetAlbumEditPreviewRequest, Result<AlbumEditPreview>>().Returns(Result<AlbumEditPreview>.Ok(Preview()));
        SetupWriteDialog(_ => null);

        // Act
        await sut.EditAlbumAsync(Album());

        // Assert
        Assert.NotNull(shown);
        Assert.Equal(genreFromArtist, shown.GenreLocked);
        GetAlbumEditPreviewRequest sent = Assert.Single(_mediator.Sent<GetAlbumEditPreviewRequest>());
        Assert.Equal(genreFromArtist ? null : "Jazz", sent.GenreName);
    }

    [Fact(DisplayName = "a_refused_write_request_gives_the_translated_message_to_the_dialog")]
    public async Task EditAlbumAsync_RefusedWrite_ReturnsTheMessage()
    {
        // Arrange
        AlbumEditService sut = BuildService();
        AlbumTagWriteAttempt? attempt = null;
        _mediator.Setup<GetAlbumEditPreviewRequest, Result<AlbumEditPreview>>().Returns(Result<AlbumEditPreview>.Ok(Preview()));
        _mediator.Setup<EditAlbumTagsRequest, Result<AlbumTagWriteOutcome>>().Returns(Result<AlbumTagWriteOutcome>.Fail(new OperationError("album.edit.pending", "pending")));
        CaptureEditDialog(Values(name: "Abbey Road (Remaster)"));
        SetupWriteDialog(_ => null, found => attempt = found);

        // Act
        await sut.EditAlbumAsync(Album());

        // Assert
        Assert.NotNull(attempt);
        Assert.Null(attempt.Outcome);
        Assert.Equal("albumEditError_Pending", attempt.Error);
    }

    [Fact(DisplayName = "validation_checks_the_values_once_and_the_edit_reuses_the_preview")]
    public async Task EditAlbumAsync_ValidatedValues_AreNotCheckedAgain()
    {
        // Arrange
        AlbumEditService sut = BuildService();
        _mediator.Setup<GetAlbumEditPreviewRequest, Result<AlbumEditPreview>>().Returns(Result<AlbumEditPreview>.Ok(Preview()));
        AlbumEditValues typed = Values(name: "Abbey Road (Remaster)");
        Func<AlbumEditValues, Task<string?>>? validate = null;
        _dialogService
            .Setup(x => x.ShowEditAlbumAsync(It.IsAny<AlbumEditValues>(), It.IsAny<AlbumTaggedFieldsMode>(), It.IsAny<AlbumEditSuggestions>(), It.IsAny<Func<AlbumEditValues, Task<string?>>>()))
            .Returns<AlbumEditValues, AlbumTaggedFieldsMode, AlbumEditSuggestions, Func<AlbumEditValues, Task<string?>>>(async (_, _, _, validateAsync) =>
            {
                validate = validateAsync;
                Assert.Null(await validateAsync(typed));

                return typed;
            });
        SetupWriteDialog(_ => null);

        // Act
        await sut.EditAlbumAsync(Album());

        // Assert
        Assert.NotNull(validate);
        Assert.Single(_mediator.Sent<GetAlbumEditPreviewRequest>());
    }

    [Theory(DisplayName = "validation_refuses_an_invalid_value_with_its_own_message")]
    [InlineData("", "The Beatles", "1969", "Rock", "albumEditError_InvalidName")]
    [InlineData("Abbey Road (Remaster)", "  ", "1969", "Rock", "albumEditError_InvalidArtist")]
    [InlineData("Abbey Road (Remaster)", "The Beatles", "abc", "Rock", "albumEditError_InvalidYear")]
    [InlineData("Abbey Road (Remaster)", "The Beatles", "0", "Rock", "albumEditError_InvalidYear")]
    [InlineData("Abbey Road (Remaster)", "The Beatles", "-5", "Rock", "albumEditError_InvalidYear")]
    public async Task Validate_InvalidValue_ReturnsTheMessage(string name, string artist, string year, string genre, string expected)
    {
        // Arrange
        AlbumEditService sut = BuildService();
        Func<AlbumEditValues, Task<string?>> validate = CaptureEditDialog(null);

        await sut.EditAlbumAsync(Album());

        // Act
        string? message = await validate(Values(name, artist, year, genre));

        // Assert
        Assert.Equal(expected, message);
        Assert.Empty(_mediator.Sent<GetAlbumEditPreviewRequest>());
    }

    [Fact(DisplayName = "validation_gives_the_message_of_a_refused_preview")]
    public async Task Validate_RefusedPreview_ReturnsTheMessageOfTheCode()
    {
        // Arrange
        AlbumEditService sut = BuildService();
        _mediator.Setup<GetAlbumEditPreviewRequest, Result<AlbumEditPreview>>().Returns(Result<AlbumEditPreview>.Fail(new OperationError("album.edit.compilation_artist", "no")));
        Func<AlbumEditValues, Task<string?>> validate = CaptureEditDialog(null);

        await sut.EditAlbumAsync(Album());

        // Act
        string? message = await validate(Values(name: "Abbey Road (Remaster)"));

        // Assert
        Assert.Equal("albumEditError_CompilationArtist", message);
    }

    [Fact(DisplayName = "validation_accepts_the_values_when_the_preview_says_nothing_changes")]
    public async Task Validate_UnchangedPreview_AcceptsTheValues()
    {
        // Arrange
        AlbumEditService sut = BuildService();
        _mediator.Setup<GetAlbumEditPreviewRequest, Result<AlbumEditPreview>>().Returns(Result<AlbumEditPreview>.Fail(new OperationError("album.edit.unchanged", "same")));
        Func<AlbumEditValues, Task<string?>> validate = CaptureEditDialog(null);

        await sut.EditAlbumAsync(Album());

        // Act
        string? message = await validate(Values(name: "ABBEY ROAD"));

        // Assert
        Assert.Null(message);
    }

    [Fact(DisplayName = "resuming_a_write_opens_the_resume_dialog_and_sends_the_resume_request")]
    public async Task ResumeTagWriteAsync_SendsTheResumeRequest()
    {
        // Arrange
        AlbumEditService sut = BuildService();
        AlbumTagWritePrompt? prompt = null;
        _mediator.Setup<ResumeAlbumTagWriteRequest, Result<AlbumTagWriteOutcome>>().Returns(Result<AlbumTagWriteOutcome>.Ok(Outcome(EArtistRenameState.Completed)));
        _dialogService
            .Setup(x => x.ShowAlbumTagWriteAsync(It.IsAny<AlbumTagWritePrompt>(), It.IsAny<Func<IProgress<ArtistRenameProgress>, Task<AlbumTagWriteAttempt>>>()))
            .Returns<AlbumTagWritePrompt, Func<IProgress<ArtistRenameProgress>, Task<AlbumTagWriteAttempt>>>(async (shown, run) =>
            {
                prompt = shown;
                AlbumTagWriteAttempt attempt = await run(new Progress<ArtistRenameProgress>());

                return attempt.Outcome;
            });

        // Act
        AlbumEditResult result = await sut.ResumeTagWriteAsync(Album());

        // Assert
        Assert.True(prompt?.IsResume);
        Assert.True(result.TagsChanged);
        Assert.False(result.Updated);
        Assert.Equal(7, Assert.Single(_mediator.Sent<ResumeAlbumTagWriteRequest>()).AlbumId);
    }

    [Fact(DisplayName = "resuming_a_write_does_nothing_when_writing_tags_is_off")]
    public async Task ResumeTagWriteAsync_OptionOff_DoesNothing()
    {
        // Arrange
        AlbumEditService sut = BuildService(writeTagsEnabled: false);

        // Act
        AlbumEditResult result = await sut.ResumeTagWriteAsync(Album());

        // Assert
        Assert.False(result.TagsChanged);
        Assert.Empty(_mediator.Sent<ResumeAlbumTagWriteRequest>());
        VerifyNoWriteDialog();
    }

    [Theory(DisplayName = "the_pending_check_is_true_only_when_the_request_says_so")]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task HasPendingTagWriteAsync_FollowsTheRequest(bool pending, bool expected)
    {
        // Arrange
        _mediator.Setup<GetAlbumPendingTagWriteRequest, Result<bool>>().Returns(Result<bool>.Ok(pending));
        AlbumEditService sut = BuildService();

        // Act
        bool result = await sut.HasPendingTagWriteAsync(7);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact(DisplayName = "the_pending_check_is_false_when_the_request_fails")]
    public async Task HasPendingTagWriteAsync_FailedRequest_IsFalse()
    {
        // Arrange
        _mediator.Setup<GetAlbumPendingTagWriteRequest, Result<bool>>().Returns(Result<bool>.Fail(NotFoundError.ForEntity("Album", 7L)));
        AlbumEditService sut = BuildService();

        // Act
        bool result = await sut.HasPendingTagWriteAsync(7);

        // Assert
        Assert.False(result);
    }
}