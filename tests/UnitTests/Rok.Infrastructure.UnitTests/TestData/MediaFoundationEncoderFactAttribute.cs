namespace Rok.Infrastructure.UnitTests.TestData;

internal sealed class MediaFoundationEncoderFactAttribute : FactAttribute
{
    public MediaFoundationEncoderFactAttribute(AudioFixtureFormat format)
    {
        if (!AudioFixtureFactory.IsEncoderAvailable(format))
            Skip = $"No Media Foundation encoder available for {format} on this machine.";
    }
}