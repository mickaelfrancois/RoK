using Rok.Domain.Enums;

namespace Rok.Application.Features.Albums.Compilation;

/// <summary>Decides the compilation status of an album from its origin and what the import detects in its files.</summary>
public static class CompilationStatusPolicy
{
    /// <summary>
    /// A manual choice always stays. A compilation flag in the files makes a compilation of origin tag.
    /// A tag-based status whose flag has vanished falls back to the artist count. A derived status only changes
    /// when the artist count switches, so an older choice is kept as long as the folder does not change.
    /// </summary>
    /// <param name="source">Current origin of the album status.</param>
    /// <param name="isCompilation">Current status of the album.</param>
    /// <param name="tagged">At least one file of the album carries the compilation flag.</param>
    /// <param name="multiArtist">The files of the album have more than one distinct artist.</param>
    /// <param name="wasMultiArtist">The album had more than one distinct artist at the previous import.</param>
    /// <returns>The status to store.</returns>
    public static CompilationStatus Resolve(ECompilationSource source, bool isCompilation, bool tagged, bool multiArtist, bool wasMultiArtist)
    {
        if (source == ECompilationSource.Manual)
            return new CompilationStatus(isCompilation, ECompilationSource.Manual);

        if (tagged)
            return new CompilationStatus(true, ECompilationSource.Tag);

        if (source == ECompilationSource.Tag || multiArtist != wasMultiArtist)
            return new CompilationStatus(multiArtist, ECompilationSource.Derived);

        return new CompilationStatus(isCompilation, ECompilationSource.Derived);
    }
}