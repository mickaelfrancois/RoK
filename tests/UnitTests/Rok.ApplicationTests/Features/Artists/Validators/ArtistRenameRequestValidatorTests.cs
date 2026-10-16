using Rok.Application.Features.Artists.Requests;

namespace Rok.ApplicationTests.Features.Artists.Validators;

public class ArtistRenameRequestValidatorTests
{
    [Theory(DisplayName = "rename_request_validator_rejects_bad_values")]
    [InlineData(0, "New")]
    [InlineData(1, "")]
    [InlineData(1, "   ")]
    public async Task RenameValidator_BadValues_Fail(long id, string name)
    {
        // Arrange
        RenameArtistRequestValidator sut = new();

        // Act
        ValidationResult result = await sut.ValidateAsync(new RenameArtistRequest(id, name), CancellationToken.None);

        // Assert
        Assert.False(result.IsValid);
    }

    [Fact(DisplayName = "rename_request_validator_rejects_a_name_over_255_characters")]
    public async Task RenameValidator_TooLong_Fails()
    {
        // Arrange
        RenameArtistRequestValidator sut = new();

        // Act
        ValidationResult result = await sut.ValidateAsync(new RenameArtistRequest(1, new string('a', 256)), CancellationToken.None);

        // Assert
        Assert.False(result.IsValid);
    }

    [Fact(DisplayName = "rename_request_validator_accepts_a_name_of_255_characters")]
    public async Task RenameValidator_MaxLength_Succeeds()
    {
        // Arrange
        RenameArtistRequestValidator sut = new();

        // Act
        ValidationResult result = await sut.ValidateAsync(new RenameArtistRequest(1, new string('a', 255)), CancellationToken.None);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory(DisplayName = "preview_request_validator_rejects_bad_values")]
    [InlineData(0, "New")]
    [InlineData(1, " ")]
    public async Task PreviewValidator_BadValues_Fail(long id, string name)
    {
        // Arrange
        GetArtistRenamePreviewRequestValidator sut = new();

        // Act
        ValidationResult result = await sut.ValidateAsync(new GetArtistRenamePreviewRequest(id, name), CancellationToken.None);

        // Assert
        Assert.False(result.IsValid);
    }

    [Fact(DisplayName = "preview_request_validator_accepts_a_valid_request")]
    public async Task PreviewValidator_ValidRequest_Succeeds()
    {
        // Arrange
        GetArtistRenamePreviewRequestValidator sut = new();

        // Act
        ValidationResult result = await sut.ValidateAsync(new GetArtistRenamePreviewRequest(1, "New"), CancellationToken.None);

        // Assert
        Assert.True(result.IsValid);
    }
}