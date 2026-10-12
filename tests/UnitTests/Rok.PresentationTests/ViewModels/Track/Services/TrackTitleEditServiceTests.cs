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

public class TrackTitleEditServiceTests
{
    private readonly FakeMediator _mediator = new();
    private readonly Mock<IDialogService> _dialogService = new();
    private readonly Mock<IStringResourceProvider> _strings = new();

    private TrackTitleEditService BuildService()
        => new(_mediator, _dialogService.Object, _strings.Object, NullLogger<TrackTitleEditService>.Instance);

    [Fact(DisplayName = "save_succeeds_returns_no_message_and_sends_one_request")]
    public async Task TrySaveAsync_Success_ReturnsNull()
    {
        // Arrange
        _mediator.Setup<UpdateTrackTitleRequest, Result<string>>().Returns(Result<string>.Ok("New"));
        TrackTitleEditService sut = BuildService();

        // Act
        string? message = await sut.TrySaveAsync(7, "New");

        // Assert
        Assert.Null(message);
        UpdateTrackTitleRequest sent = Assert.Single(_mediator.Sent<UpdateTrackTitleRequest>());
        Assert.Equal(7, sent.TrackId);
        Assert.Equal("New", sent.Title);
    }

    [Fact(DisplayName = "refusal_returns_the_translated_message_of_its_reason")]
    public async Task TrySaveAsync_Refusal_ReturnsTranslatedMessage()
    {
        // Arrange
        string code = TrackTagEditResult.CodeFor(ETagWriteReason.ReadOnly);
        _mediator.Setup<UpdateTrackTitleRequest, Result<string>>().Returns(Result<string>.Fail(new OperationError(code, "read only")));
        _strings.Setup(strings => strings.GetString("trackEditError_ReadOnly")).Returns("The file is read-only.");
        TrackTitleEditService sut = BuildService();

        // Act
        string? message = await sut.TrySaveAsync(7, "New");

        // Assert
        Assert.Equal("The file is read-only.", message);
    }

    [Fact(DisplayName = "refusal_with_unknown_code_returns_the_generic_message")]
    public async Task TrySaveAsync_UnknownCode_ReturnsGenericMessage()
    {
        // Arrange
        _mediator.Setup<UpdateTrackTitleRequest, Result<string>>().Returns(Result<string>.Fail(new OperationError("other", "other")));
        _strings.Setup(strings => strings.GetString("trackEditError_Generic")).Returns("Generic.");
        TrackTitleEditService sut = BuildService();

        // Act
        string? message = await sut.TrySaveAsync(7, "New");

        // Assert
        Assert.Equal("Generic.", message);
    }

    [Fact(DisplayName = "missing_translation_falls_back_to_the_generic_message")]
    public async Task TrySaveAsync_MissingTranslation_FallsBackToGeneric()
    {
        // Arrange
        string code = TrackTagEditResult.CodeFor(ETagWriteReason.Locked);
        _mediator.Setup<UpdateTrackTitleRequest, Result<string>>().Returns(Result<string>.Fail(new OperationError(code, "locked")));
        _strings.Setup(strings => strings.GetString("trackEditError_Locked")).Returns(string.Empty);
        _strings.Setup(strings => strings.GetString("trackEditError_Generic")).Returns("Generic.");
        TrackTitleEditService sut = BuildService();

        // Act
        string? message = await sut.TrySaveAsync(7, "New");

        // Assert
        Assert.Equal("Generic.", message);
    }

    [Fact(DisplayName = "edit_opens_the_dialog_with_the_current_title_and_returns_the_saved_title")]
    public async Task EditTitleAsync_ReturnsSavedTitle()
    {
        // Arrange
        _dialogService.Setup(dialog => dialog.ShowEditTrackTitleAsync("Old", It.IsAny<Func<string, Task<string?>>>())).ReturnsAsync("New");
        TrackTitleEditService sut = BuildService();

        // Act
        string? title = await sut.EditTitleAsync(7, "Old");

        // Assert
        Assert.Equal("New", title);
    }
}