using Rok.Application.Features.Albums.Requests;

namespace Rok.ApplicationTests.Features.Albums.Validators;

public class AlbumMergeRequestValidatorTests
{
    [Theory(DisplayName = "merge_and_preview_accept_positive_ids")]
    [InlineData(1, 2)]
    [InlineData(5, 5)]
    public async Task Validate_PositiveIds_Succeeds(long sourceId, long targetId)
    {
        ValidationResult merge = await new MergeAlbumsRequestValidator().ValidateAsync(new MergeAlbumsRequest(sourceId, targetId, false), CancellationToken.None);
        ValidationResult preview = await new GetAlbumMergePreviewRequestValidator().ValidateAsync(new GetAlbumMergePreviewRequest(sourceId, targetId), CancellationToken.None);

        Assert.True(merge.IsValid);
        Assert.True(preview.IsValid);
    }

    [Theory(DisplayName = "merge_and_preview_fail_when_an_id_is_not_positive")]
    [InlineData(0, 2)]
    [InlineData(1, 0)]
    [InlineData(-1, -2)]
    public async Task Validate_IdNotPositive_Fails(long sourceId, long targetId)
    {
        ValidationResult merge = await new MergeAlbumsRequestValidator().ValidateAsync(new MergeAlbumsRequest(sourceId, targetId, true), CancellationToken.None);
        ValidationResult preview = await new GetAlbumMergePreviewRequestValidator().ValidateAsync(new GetAlbumMergePreviewRequest(sourceId, targetId), CancellationToken.None);

        Assert.False(merge.IsValid);
        Assert.False(preview.IsValid);
    }
}