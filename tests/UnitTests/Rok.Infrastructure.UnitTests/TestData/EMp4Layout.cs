namespace Rok.Infrastructure.UnitTests.TestData;

/// <summary>Position of the <c>moov</c> box relative to the first <c>mdat</c> box of an MP4 file.</summary>
public enum EMp4Layout
{
    MoovBeforeMdat,
    MoovAfterMdat
}