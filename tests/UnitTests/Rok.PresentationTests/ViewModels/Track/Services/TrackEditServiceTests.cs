using CleanArch.DevKit.Mediator.Results;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Errors;
using Rok.Application.Features.Tracks.Requests;
using Rok.Application.Features.Tracks.Services;
using Rok.Application.Tag;
using Rok.Services;
using Rok.ViewModels.Track.Services;

namespace Rok.PresentationTests.ViewModels.Track.Services;

public class TrackEditServiceTests
{
    private static readonly TrackEditSaved Saved = new("New", 4, 1, "Composer", 130, new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc), 1234);

    private readonly FakeMediator _mediator = new();
    private readonly Mock<IDialogService> _dialogService = new();
    private readonly Mock<IStringResourceProvider> _strings = new();

    private TrackEditService BuildService()
        => new(_mediator, _dialogService.Object, _strings.Object, NullLogger<TrackEditService>.Instance);

    [Fact(DisplayName = "save_succeeds_returns_the_saved_values_and_sends_one_request")]
    public async Task TrySaveAsync_Success_ReturnsSaved()
    {
        // Arrange
        _mediator.Setup<UpdateTrackTagsRequest, Result<TrackEditSaved>>().Returns(Result<TrackEditSaved>.Ok(Saved));
        TrackEditService sut = BuildService();

        // Act
        TrackEditAttempt attempt = await sut.TrySaveAsync(new TrackTagsEdit(7, "New", 4, 1, "Composer", 130));

        // Assert
        Assert.Equal(Saved, attempt.Saved);
        Assert.Null(attempt.Error);
        UpdateTrackTagsRequest sent = Assert.Single(_mediator.Sent<UpdateTrackTagsRequest>());
        Assert.Equal(7, sent.TrackId);
        Assert.Equal("New", sent.Title);
        Assert.Equal(4, sent.TrackNumber);
        Assert.Equal(1, sent.Disc);
        Assert.Equal("Composer", sent.Composers);
        Assert.Equal(130, sent.Bpm);
    }

    [Fact(DisplayName = "refusal_returns_the_translated_message_of_its_reason")]
    public async Task TrySaveAsync_Refusal_ReturnsTranslatedMessage()
    {
        // Arrange
        string code = TrackTagEditResult.CodeFor(ETagWriteReason.ReadOnly);
        _mediator.Setup<UpdateTrackTagsRequest, Result<TrackEditSaved>>().Returns(Result<TrackEditSaved>.Fail(new OperationError(code, "read only")));
        _strings.Setup(strings => strings.GetString("trackEditError_ReadOnly")).Returns("The file is read-only.");
        TrackEditService sut = BuildService();

        // Act
        TrackEditAttempt attempt = await sut.TrySaveAsync(new TrackTagsEdit(7, "New"));

        // Assert
        Assert.Null(attempt.Saved);
        Assert.Equal("The file is read-only.", attempt.Error);
    }

    [Fact(DisplayName = "refusal_with_unknown_code_returns_the_generic_message")]
    public async Task TrySaveAsync_UnknownCode_ReturnsGenericMessage()
    {
        // Arrange
        _mediator.Setup<UpdateTrackTagsRequest, Result<TrackEditSaved>>().Returns(Result<TrackEditSaved>.Fail(new OperationError("other", "other")));
        _strings.Setup(strings => strings.GetString("trackEditError_Generic")).Returns("Generic.");
        TrackEditService sut = BuildService();

        // Act
        TrackEditAttempt attempt = await sut.TrySaveAsync(new TrackTagsEdit(7, "New"));

        // Assert
        Assert.Equal("Generic.", attempt.Error);
    }

    [Fact(DisplayName = "missing_translation_falls_back_to_the_generic_message")]
    public async Task TrySaveAsync_MissingTranslation_FallsBackToGeneric()
    {
        // Arrange
        string code = TrackTagEditResult.CodeFor(ETagWriteReason.Locked);
        _mediator.Setup<UpdateTrackTagsRequest, Result<TrackEditSaved>>().Returns(Result<TrackEditSaved>.Fail(new OperationError(code, "locked")));
        _strings.Setup(strings => strings.GetString("trackEditError_Locked")).Returns(string.Empty);
        _strings.Setup(strings => strings.GetString("trackEditError_Generic")).Returns("Generic.");
        TrackEditService sut = BuildService();

        // Act
        TrackEditAttempt attempt = await sut.TrySaveAsync(new TrackTagsEdit(7, "New"));

        // Assert
        Assert.Equal("Generic.", attempt.Error);
    }

    [Fact(DisplayName = "edit_opens_the_dialog_with_the_current_values_and_returns_the_saved_values")]
    public async Task EditAsync_ReturnsSavedValues()
    {
        // Arrange
        TrackEditValues current = new(7, "Old", 3, 1, "Composer", 120);
        _dialogService.Setup(dialog => dialog.ShowEditTrackAsync(current, It.IsAny<Func<TrackTagsEdit, Task<TrackEditAttempt>>>())).ReturnsAsync(Saved);
        TrackEditService sut = BuildService();

        // Act
        TrackEditSaved? saved = await sut.EditAsync(current);

        // Assert
        Assert.Equal(Saved, saved);
    }
}