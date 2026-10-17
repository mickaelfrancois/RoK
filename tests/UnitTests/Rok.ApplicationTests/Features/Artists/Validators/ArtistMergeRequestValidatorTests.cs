using Rok.Application.Features.Artists.Requests;

namespace Rok.ApplicationTests.Features.Artists.Validators;

public class ArtistMergeRequestValidatorTests
{
    [Theory(DisplayName = "merge_request_validator_rejects_bad_ids")]
    [InlineData(0, 2)]
    [InlineData(1, 0)]
    [InlineData(-1, 2)]
    public async Task MergeValidator_BadIds_Fail(long sourceId, long targetId)
    {
        // Arrange
        MergeArtistsRequestValidator sut = new();

        // Act
        ValidationResult result = await sut.ValidateAsync(new MergeArtistsRequest(sourceId, targetId), CancellationToken.None);

        // Assert
        Assert.False(result.IsValid);
    }

    [Fact(DisplayName = "merge_request_validator_accepts_a_valid_request")]
    public async Task MergeValidator_ValidRequest_Succeeds()
    {
        // Arrange
        MergeArtistsRequestValidator sut = new();

        // Act
        ValidationResult result = await sut.ValidateAsync(new MergeArtistsRequest(1, 2), CancellationToken.None);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory(DisplayName = "merge_preview_request_validator_rejects_bad_ids")]
    [InlineData(0, 2)]
    [InlineData(1, 0)]
    public async Task PreviewValidator_BadIds_Fail(long sourceId, long targetId)
    {
        // Arrange
        GetArtistMergePreviewRequestValidator sut = new();

        // Act
        ValidationResult result = await sut.ValidateAsync(new GetArtistMergePreviewRequest(sourceId, targetId), CancellationToken.None);

        // Assert
        Assert.False(result.IsValid);
    }

    [Fact(DisplayName = "merge_preview_request_validator_accepts_a_valid_request")]
    public async Task PreviewValidator_ValidRequest_Succeeds()
    {
        // Arrange
        GetArtistMergePreviewRequestValidator sut = new();

        // Act
        ValidationResult result = await sut.ValidateAsync(new GetArtistMergePreviewRequest(1, 2), CancellationToken.None);

        // Assert
        Assert.True(result.IsValid);
    }
}