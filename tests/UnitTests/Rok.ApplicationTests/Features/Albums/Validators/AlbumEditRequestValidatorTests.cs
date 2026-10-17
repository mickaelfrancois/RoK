using Rok.Application.Features.Albums.Requests;

namespace Rok.ApplicationTests.Features.Albums.Validators;

public class AlbumEditRequestValidatorTests
{
    private static async Task<bool> IsValidEditAsync(long id = 1, string name = "Name", string artist = "Artist", int? year = null, string? genre = null)
    {
        ValidationResult result = await new EditAlbumTagsRequestValidator().ValidateAsync(new EditAlbumTagsRequest(id, name, artist, year, genre), CancellationToken.None);

        return result.IsValid;
    }

    private static async Task<bool> IsValidPreviewAsync(long id = 1, string name = "Name", string artist = "Artist", int? year = null, string? genre = null)
    {
        ValidationResult result = await new GetAlbumEditPreviewRequestValidator().ValidateAsync(new GetAlbumEditPreviewRequest(id, name, artist, year, genre), CancellationToken.None);

        return result.IsValid;
    }

    [Fact(DisplayName = "edit_and_preview_accept_valid_values")]
    public async Task Validate_ValidValues_Succeeds()
    {
        Assert.True(await IsValidEditAsync(year: 1999, genre: "Rock"));
        Assert.True(await IsValidPreviewAsync(year: 9999, genre: "Rock"));
        Assert.True(await IsValidEditAsync(year: 1));
    }

    [Fact(DisplayName = "edit_and_preview_fail_when_the_id_is_not_positive")]
    public async Task Validate_IdNotPositive_Fails()
    {
        Assert.False(await IsValidEditAsync(id: 0));
        Assert.False(await IsValidPreviewAsync(id: -1));
    }

    [Theory(DisplayName = "edit_and_preview_fail_when_the_name_or_the_artist_is_blank")]
    [InlineData("", "Artist")]
    [InlineData("   ", "Artist")]
    [InlineData("Name", "")]
    [InlineData("Name", "  ")]
    public async Task Validate_BlankNameOrArtist_Fails(string name, string artist)
    {
        Assert.False(await IsValidEditAsync(name: name, artist: artist));
        Assert.False(await IsValidPreviewAsync(name: name, artist: artist));
    }

    [Fact(DisplayName = "edit_and_preview_fail_when_a_name_exceeds_255_characters")]
    public async Task Validate_TooLongNames_Fails()
    {
        string tooLong = new('a', 256);

        Assert.False(await IsValidEditAsync(name: tooLong));
        Assert.False(await IsValidEditAsync(artist: tooLong));
        Assert.False(await IsValidPreviewAsync(name: tooLong));
        Assert.True(await IsValidEditAsync(name: new string('a', 255)));
    }

    [Theory(DisplayName = "edit_and_preview_fail_when_the_year_is_out_of_range")]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(10000)]
    public async Task Validate_YearOutOfRange_Fails(int year)
    {
        Assert.False(await IsValidEditAsync(year: year));
        Assert.False(await IsValidPreviewAsync(year: year));
    }

    [Theory(DisplayName = "edit_and_preview_fail_when_the_genre_is_blank")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Validate_BlankGenre_Fails(string genre)
    {
        Assert.False(await IsValidEditAsync(genre: genre));
        Assert.False(await IsValidPreviewAsync(genre: genre));
    }

    [Fact(DisplayName = "edit_fails_when_the_genre_exceeds_255_characters")]
    public async Task Validate_TooLongGenre_Fails()
    {
        Assert.False(await IsValidEditAsync(genre: new string('g', 256)));
    }

    [Fact(DisplayName = "resume_fails_when_the_id_is_not_positive")]
    public async Task ValidateResume_IdNotPositive_Fails()
    {
        ResumeAlbumTagWriteRequestValidator sut = new();

        ValidationResult invalid = await sut.ValidateAsync(new ResumeAlbumTagWriteRequest(0), CancellationToken.None);
        ValidationResult valid = await sut.ValidateAsync(new ResumeAlbumTagWriteRequest(3), CancellationToken.None);

        Assert.False(invalid.IsValid);
        Assert.True(valid.IsValid);
    }
}