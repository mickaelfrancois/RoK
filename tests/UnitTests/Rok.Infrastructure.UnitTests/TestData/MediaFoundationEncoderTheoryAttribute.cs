namespace Rok.Infrastructure.UnitTests.TestData;

internal sealed class MediaFoundationEncoderTheoryAttribute : TheoryAttribute
{
    public MediaFoundationEncoderTheoryAttribute(AudioFixtureFormat format)
    {
        if (!AudioFixtureFactory.IsEncoderAvailable(format))
            Skip = $"No Media Foundation encoder available for {format} on this machine.";
    }
}