using CleanArch.DevKit.Mediator.Results;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Dto;
using Rok.Application.Errors;
using Rok.Application.Features.Albums.Compilation;
using Rok.Application.Features.Albums.Requests;
using Rok.Application.Features.Artists.Rename;
using Rok.Application.Interfaces;
using Rok.Services;
using Rok.ViewModels.Album.Services;
using AlbumEditPreview = Rok.Application.Features.Albums.Edit.AlbumEditPreview;
using AlbumTagWriteOutcome = Rok.Application.Features.Albums.Edit.AlbumTagWriteOutcome;

namespace Rok.PresentationTests.ViewModels.Album.Services;

public class AlbumEditServiceCompilationTests
{
    private readonly FakeMediator _mediator = new();
    private readonly Mock<IDialogService> _dialogService = new();
    private readonly Mock<IAppOptions> _appOptions = new();
    private readonly Mock<IStringResourceProvider> _strings = new();
    private readonly List<string> _calls = [];

    private AlbumEditService BuildService(bool writeTagsEnabled)
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

    private static AlbumEditValues Values(bool isCompilation, string name = "Abbey Road") => new()
    {
        Name = name,
        ArtistName = "The Beatles",
        Year = "1969",
        GenreName = "Rock",
        IsCompilation = isCompilation
    };

    private static AlbumTagWriteOutcome Outcome(EArtistRenameState state) => new(state, 7, [], [], [3], []);

    private static AlbumEditPreview Preview() => new(
        7, "Abbey Road", "Abbey Road (Remaster)", "The Beatles", "The Beatles", false, 1969, 1969, "Rock", "Rock", false, 3, 0, 0, 0, null, null, []);

    private void SetupEditDialog(AlbumEditValues? returned)
    {
        _dialogService
            .Setup(x => x.ShowEditAlbumAsync(It.IsAny<AlbumEditValues>(), It.IsAny<AlbumTaggedFieldsMode>(), It.IsAny<AlbumEditSuggestions>(), It.IsAny<Func<AlbumEditValues, Task<string?>>>()))
            .ReturnsAsync(returned);
    }

    private void SetupWriteDialog()
    {
        _dialogService
            .Setup(x => x.ShowAlbumTagWriteAsync(It.IsAny<AlbumTagWritePrompt>(), It.IsAny<Func<IProgress<ArtistRenameProgress>, Task<AlbumTagWriteAttempt>>>()))
            .Returns<AlbumTagWritePrompt, Func<IProgress<ArtistRenameProgress>, Task<AlbumTagWriteAttempt>>>(async (_, run) =>
            {
                _calls.Add("dialog");
                AlbumTagWriteAttempt attempt = await run(new Progress<ArtistRenameProgress>());

                return attempt.Outcome;
            });
    }

    private void VerifyNoWriteDialog()
    {
        _dialogService.Verify(x => x.ShowAlbumTagWriteAsync(It.IsAny<AlbumTagWritePrompt>(), It.IsAny<Func<IProgress<ArtistRenameProgress>, Task<AlbumTagWriteAttempt>>>()), Times.Never);
    }

    [Theory(DisplayName = "the_compilation_request_carries_the_checked_value")]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task EditAlbumAsync_CompilationToggled_SendsTheRequestWithTheValue(bool before, bool after)
    {
        // Arrange
        AlbumEditService sut = BuildService(writeTagsEnabled: false);
        _mediator.Setup<SetAlbumCompilationRequest, Result<AlbumTagWriteOutcome>>().Returns(Result<AlbumTagWriteOutcome>.Ok(Outcome(EArtistRenameState.Completed)));
        SetupEditDialog(Values(after));
        AlbumDto album = Album(before);

        // Act
        AlbumEditResult result = await sut.EditAlbumAsync(album);

        // Assert
        SetAlbumCompilationRequest sent = Assert.Single(_mediator.Sent<SetAlbumCompilationRequest>());
        Assert.Equal(7, sent.AlbumId);
        Assert.Equal(after, sent.IsCompilation);
        Assert.Equal(after, album.IsCompilation);
        Assert.True(result.Updated);
    }

    [Fact(DisplayName = "nothing_is_sent_when_the_compilation_status_is_unchanged")]
    public async Task EditAlbumAsync_CompilationUnchanged_SendsNoCompilationRequest()
    {
        // Arrange
        AlbumEditService sut = BuildService(writeTagsEnabled: true);
        SetupEditDialog(Values(isCompilation: true));

        // Act
        await sut.EditAlbumAsync(Album(isCompilation: true));

        // Assert
        Assert.Empty(_mediator.Sent<SetAlbumCompilationRequest>());
        VerifyNoWriteDialog();
    }

    [Fact(DisplayName = "tags_disabled_sends_the_compilation_request_without_the_write_dialog")]
    public async Task EditAlbumAsync_TagsDisabled_SendsDirectlyWithoutDialog()
    {
        // Arrange
        AlbumEditService sut = BuildService(writeTagsEnabled: false);
        _mediator.Setup<SetAlbumCompilationRequest, Result<AlbumTagWriteOutcome>>().Returns(Result<AlbumTagWriteOutcome>.Ok(Outcome(EArtistRenameState.Completed)));
        SetupEditDialog(Values(isCompilation: true));

        // Act
        await sut.EditAlbumAsync(Album());

        // Assert
        Assert.Single(_mediator.Sent<SetAlbumCompilationRequest>());
        VerifyNoWriteDialog();
    }

    [Fact(DisplayName = "tags_enabled_sends_the_compilation_request_through_the_write_dialog")]
    public async Task EditAlbumAsync_TagsEnabled_SendsThroughTheWriteDialog()
    {
        // Arrange
        AlbumEditService sut = BuildService(writeTagsEnabled: true);
        _mediator.Setup<SetAlbumCompilationRequest, Result<AlbumTagWriteOutcome>>().Returns(Result<AlbumTagWriteOutcome>.Ok(Outcome(EArtistRenameState.Completed)));
        SetupEditDialog(Values(isCompilation: true));
        SetupWriteDialog();
        AlbumDto album = Album();

        // Act
        AlbumEditResult result = await sut.EditAlbumAsync(album);

        // Assert
        Assert.Single(_mediator.Sent<SetAlbumCompilationRequest>());
        Assert.Single(_calls);
        Assert.True(album.IsCompilation);
        Assert.Equal(EArtistRenameState.Completed, result.Outcome?.State);
    }

    [Fact(DisplayName = "the_tagged_edit_is_written_before_the_compilation_change")]
    public async Task EditAlbumAsync_TaggedEditAndCompilation_TaggedEditComesFirst()
    {
        // Arrange
        AlbumEditService sut = BuildService(writeTagsEnabled: true);
        _mediator.Setup<GetAlbumEditPreviewRequest, Result<AlbumEditPreview>>().Returns(Result<AlbumEditPreview>.Ok(Preview()));
        _mediator.Setup<EditAlbumTagsRequest, Result<AlbumTagWriteOutcome>>().Returns(Result<AlbumTagWriteOutcome>.Ok(Outcome(EArtistRenameState.Completed)));
        _mediator.Setup<SetAlbumCompilationRequest, Result<AlbumTagWriteOutcome>>().Returns(Result<AlbumTagWriteOutcome>.Ok(Outcome(EArtistRenameState.Completed)));
        SetupEditDialog(Values(isCompilation: true, name: "Abbey Road (Remaster)"));

        _dialogService
            .Setup(x => x.ShowAlbumTagWriteAsync(It.IsAny<AlbumTagWritePrompt>(), It.IsAny<Func<IProgress<ArtistRenameProgress>, Task<AlbumTagWriteAttempt>>>()))
            .Returns<AlbumTagWritePrompt, Func<IProgress<ArtistRenameProgress>, Task<AlbumTagWriteAttempt>>>(async (_, run) =>
            {
                AlbumTagWriteAttempt attempt = await run(new Progress<ArtistRenameProgress>());
                _calls.Add(_mediator.Sent<SetAlbumCompilationRequest>().Any() ? "compilation" : "tagged");

                return attempt.Outcome;
            });

        // Act
        await sut.EditAlbumAsync(Album());

        // Assert
        Assert.Equal(["tagged", "compilation"], _calls);
        Assert.Single(_mediator.Sent<EditAlbumTagsRequest>());
        Assert.Single(_mediator.Sent<SetAlbumCompilationRequest>());
    }

    [Fact(DisplayName = "the_compilation_change_is_skipped_when_the_tagged_edit_is_incomplete")]
    public async Task EditAlbumAsync_TaggedEditPartial_SkipsTheCompilationChange()
    {
        // Arrange
        AlbumEditService sut = BuildService(writeTagsEnabled: true);
        _mediator.Setup<GetAlbumEditPreviewRequest, Result<AlbumEditPreview>>().Returns(Result<AlbumEditPreview>.Ok(Preview()));
        _mediator.Setup<EditAlbumTagsRequest, Result<AlbumTagWriteOutcome>>().Returns(Result<AlbumTagWriteOutcome>.Ok(Outcome(EArtistRenameState.Partial)));
        SetupEditDialog(Values(isCompilation: true, name: "Abbey Road (Remaster)"));
        SetupWriteDialog();
        AlbumDto album = Album();

        // Act
        AlbumEditResult result = await sut.EditAlbumAsync(album);

        // Assert
        Assert.Empty(_mediator.Sent<SetAlbumCompilationRequest>());
        Assert.False(album.IsCompilation);
        Assert.Equal(EArtistRenameState.Partial, result.Outcome?.State);
    }

    [Fact(DisplayName = "a_pending_refusal_is_shown_and_the_album_keeps_its_status")]
    public async Task EditAlbumAsync_PendingRefusal_ShowsTheMessageAndKeepsTheStatus()
    {
        // Arrange
        AlbumEditService sut = BuildService(writeTagsEnabled: false);
        _mediator.Setup<SetAlbumCompilationRequest, Result<AlbumTagWriteOutcome>>().Returns(Result<AlbumTagWriteOutcome>.Fail(new OperationError("album.edit.pending", "pending")));
        SetupEditDialog(Values(isCompilation: true));
        AlbumDto album = Album();

        // Act
        AlbumEditResult result = await sut.EditAlbumAsync(album);

        // Assert
        _dialogService.Verify(x => x.ShowTextAsync("Abbey Road", AlbumEditTextKeys.Error("album.edit.pending")), Times.Once);
        Assert.False(album.IsCompilation);
        Assert.Null(result.Outcome);
    }

    [Fact(DisplayName = "a_partial_compilation_write_is_resumed_with_the_resume_request")]
    public async Task EditAlbumAsync_CompilationPartialThenRetry_ResumesTheWrite()
    {
        // Arrange
        AlbumEditService sut = BuildService(writeTagsEnabled: true);
        _mediator.Setup<SetAlbumCompilationRequest, Result<AlbumTagWriteOutcome>>().Returns(Result<AlbumTagWriteOutcome>.Ok(Outcome(EArtistRenameState.Partial)));
        _mediator.Setup<ResumeAlbumTagWriteRequest, Result<AlbumTagWriteOutcome>>().Returns(Result<AlbumTagWriteOutcome>.Ok(Outcome(EArtistRenameState.Completed)));
        SetupEditDialog(Values(isCompilation: true));

        _dialogService
            .Setup(x => x.ShowAlbumTagWriteAsync(It.IsAny<AlbumTagWritePrompt>(), It.IsAny<Func<IProgress<ArtistRenameProgress>, Task<AlbumTagWriteAttempt>>>()))
            .Returns<AlbumTagWritePrompt, Func<IProgress<ArtistRenameProgress>, Task<AlbumTagWriteAttempt>>>(async (_, run) =>
            {
                await run(new Progress<ArtistRenameProgress>());
                AlbumTagWriteAttempt second = await run(new Progress<ArtistRenameProgress>());

                return second.Outcome;
            });

        AlbumDto album = Album();

        // Act
        AlbumEditResult result = await sut.EditAlbumAsync(album);

        // Assert
        Assert.Single(_mediator.Sent<SetAlbumCompilationRequest>());
        Assert.Single(_mediator.Sent<ResumeAlbumTagWriteRequest>());
        Assert.True(album.IsCompilation);
        Assert.Equal(EArtistRenameState.Completed, result.Outcome?.State);
    }

    [Theory(DisplayName = "the_compilation_error_codes_map_to_their_own_text")]
    [InlineData("album.compilation.unchanged")]
    [InlineData("album.compilation.no_artist")]
    [InlineData("album.compilation.collision")]
    [InlineData("album.edit.pending")]
    public void ErrorKey_CompilationCode_IsNotGeneric(string code)
    {
        // Act
        string key = AlbumEditTextKeys.Error(code);

        // Assert
        Assert.NotEqual(AlbumEditTextKeys.GenericError, key);
        Assert.Contains(key, AlbumEditTextKeys.AllKeys);
    }
}