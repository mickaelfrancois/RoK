using Rok.Application.Features.Albums.Compilation;
using Rok.Domain.Enums;

namespace Rok.ApplicationTests.Features.Albums.Compilation;

public class CompilationStatusPolicyTests
{
    [Theory(DisplayName = "manual_choice_is_never_changed")]
    [InlineData(true, true, true, true)]
    [InlineData(true, true, true, false)]
    [InlineData(true, true, false, true)]
    [InlineData(true, true, false, false)]
    [InlineData(true, false, true, true)]
    [InlineData(true, false, true, false)]
    [InlineData(true, false, false, true)]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, true, true)]
    [InlineData(false, true, true, false)]
    [InlineData(false, true, false, true)]
    [InlineData(false, true, false, false)]
    [InlineData(false, false, true, true)]
    [InlineData(false, false, true, false)]
    [InlineData(false, false, false, true)]
    [InlineData(false, false, false, false)]
    public void Resolve_Manual_KeepsStatusAndSource(bool isCompilation, bool tagged, bool multiArtist, bool wasMultiArtist)
    {
        // Act
        CompilationStatus status = CompilationStatusPolicy.Resolve(ECompilationSource.Manual, isCompilation, tagged, multiArtist, wasMultiArtist);

        // Assert
        Assert.Equal(new CompilationStatus(isCompilation, ECompilationSource.Manual), status);
    }

    [Theory(DisplayName = "tagged_album_is_a_tag_compilation")]
    [InlineData(ECompilationSource.Derived, true, true, true)]
    [InlineData(ECompilationSource.Derived, true, true, false)]
    [InlineData(ECompilationSource.Derived, true, false, true)]
    [InlineData(ECompilationSource.Derived, true, false, false)]
    [InlineData(ECompilationSource.Derived, false, true, true)]
    [InlineData(ECompilationSource.Derived, false, true, false)]
    [InlineData(ECompilationSource.Derived, false, false, true)]
    [InlineData(ECompilationSource.Derived, false, false, false)]
    [InlineData(ECompilationSource.Tag, true, true, true)]
    [InlineData(ECompilationSource.Tag, true, true, false)]
    [InlineData(ECompilationSource.Tag, true, false, true)]
    [InlineData(ECompilationSource.Tag, true, false, false)]
    [InlineData(ECompilationSource.Tag, false, true, true)]
    [InlineData(ECompilationSource.Tag, false, true, false)]
    [InlineData(ECompilationSource.Tag, false, false, true)]
    [InlineData(ECompilationSource.Tag, false, false, false)]
    public void Resolve_Tagged_BecomesTagCompilation(ECompilationSource source, bool isCompilation, bool multiArtist, bool wasMultiArtist)
    {
        // Act
        CompilationStatus status = CompilationStatusPolicy.Resolve(source, isCompilation, true, multiArtist, wasMultiArtist);

        // Assert
        Assert.Equal(new CompilationStatus(true, ECompilationSource.Tag), status);
    }

    [Theory(DisplayName = "vanished_tag_falls_back_to_the_artist_count")]
    [InlineData(true, true, true)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, true)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    [InlineData(false, false, false)]
    public void Resolve_TagSourceWithoutFlag_RecomputesFromArtistCount(bool isCompilation, bool multiArtist, bool wasMultiArtist)
    {
        // Act
        CompilationStatus status = CompilationStatusPolicy.Resolve(ECompilationSource.Tag, isCompilation, false, multiArtist, wasMultiArtist);

        // Assert
        Assert.Equal(new CompilationStatus(multiArtist, ECompilationSource.Derived), status);
    }

    [Theory(DisplayName = "derived_status_follows_a_switch_of_the_artist_count")]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void Resolve_DerivedWithSwitch_FollowsArtistCount(bool isCompilation, bool multiArtist, bool wasMultiArtist)
    {
        // Act
        CompilationStatus status = CompilationStatusPolicy.Resolve(ECompilationSource.Derived, isCompilation, false, multiArtist, wasMultiArtist);

        // Assert
        Assert.Equal(new CompilationStatus(multiArtist, ECompilationSource.Derived), status);
    }

    [Theory(DisplayName = "derived_status_is_kept_without_a_switch")]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void Resolve_DerivedWithoutSwitch_KeepsStatus(bool isCompilation, bool multiArtist)
    {
        // Act
        CompilationStatus status = CompilationStatusPolicy.Resolve(ECompilationSource.Derived, isCompilation, false, multiArtist, multiArtist);

        // Assert
        Assert.Equal(new CompilationStatus(isCompilation, ECompilationSource.Derived), status);
    }

    [Fact(DisplayName = "derived_compilation_with_one_artist_left_is_no_longer_a_compilation")]
    public void Resolve_DerivedCompilationDownToOneArtist_IsNotACompilation()
    {
        // Act
        CompilationStatus status = CompilationStatusPolicy.Resolve(ECompilationSource.Derived, true, false, false, true);

        // Assert
        Assert.False(status.IsCompilation);
    }
}