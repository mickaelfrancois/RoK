using Rok.Services.PlayerCommand.Api;

namespace Rok.PresentationTests.Services.PlayerCommand.Api;

public class WebApiResultTests
{
    [Fact(DisplayName = "Ok should produce a 200 result with empty body by default")]
    public void Ok_ShouldProduceTwoHundredWithEmptyBody()
    {
        // Act
        WebApiResult result = WebApiResult.Ok();

        // Assert
        Assert.Equal(200, result.StatusCode);
        Assert.Equal("", result.Body);
    }

    [Fact(DisplayName = "Ok should embed the provided body when supplied")]
    public void Ok_ShouldEmbedProvidedBody()
    {
        // Act
        WebApiResult result = WebApiResult.Ok("hello");

        // Assert
        Assert.Equal(200, result.StatusCode);
        Assert.Equal("hello", result.Body);
    }

    [Fact(DisplayName = "NotFound should produce a 404 result with empty body by default")]
    public void NotFound_ShouldProduceFourOhFourWithEmptyBody()
    {
        // Act
        WebApiResult result = WebApiResult.NotFound();

        // Assert
        Assert.Equal(404, result.StatusCode);
        Assert.Equal("", result.Body);
    }

    [Fact(DisplayName = "NotFound should embed the provided body when supplied")]
    public void NotFound_ShouldEmbedProvidedBody()
    {
        // Act
        WebApiResult result = WebApiResult.NotFound("Album not found");

        // Assert
        Assert.Equal(404, result.StatusCode);
        Assert.Equal("Album not found", result.Body);
    }

    [Fact(DisplayName = "BadRequest should produce a 400 result with empty body")]
    public void BadRequest_ShouldProduceFourHundredWithEmptyBody()
    {
        // Act
        WebApiResult result = WebApiResult.BadRequest();

        // Assert
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("", result.Body);
    }
}
