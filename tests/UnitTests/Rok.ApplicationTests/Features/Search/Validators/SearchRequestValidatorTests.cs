using Rok.Application.Features.Search.Requests;

namespace Rok.ApplicationTests.Features.Search.Validators;

public class SearchRequestValidatorTests
{
    [Fact(DisplayName = "fails_when_name_is_empty")]
    public async Task fails_when_name_is_empty()
    {
        SearchRequestValidator sut = new();
        SearchRequest request = new() { Name = string.Empty };

        ValidationResult result = await sut.ValidateAsync(request, CancellationToken.None);

        Assert.False(result.IsValid);
    }

    [Fact(DisplayName = "succeeds_when_name_is_provided")]
    public async Task succeeds_when_name_is_provided()
    {
        SearchRequestValidator sut = new();
        SearchRequest request = new() { Name = "Pink Floyd" };

        ValidationResult result = await sut.ValidateAsync(request, CancellationToken.None);

        Assert.True(result.IsValid);
    }
}
