using Rok.ViewModels.Start;

namespace Rok.PresentationTests.ViewModels.Start;

public sealed class UnsupportedAudioSummaryTests
{
    private const string PluralTemplate = "{0} {1} files found";
    private const string SingularTemplate = "{0} {1} file found";
    private const string Fallback = "No compatible audio files";

    [Fact(DisplayName = "dominant_should_return_extension_with_highest_count")]
    public void TryGetDominant_ReturnsExtensionWithHighestCount()
    {
        // Arrange
        Dictionary<string, int> counts = new() { [".m4a"] = 312, [".wma"] = 4 };

        // Act
        bool found = UnsupportedAudioSummary.TryGetDominant(counts, out string extension, out int count);

        // Assert
        Assert.True(found);
        Assert.Equal(".m4a", extension);
        Assert.Equal(312, count);
    }

    [Fact(DisplayName = "dominant_should_break_ties_by_ordinal_extension")]
    public void TryGetDominant_BreaksTiesByOrdinalExtension()
    {
        // Arrange
        Dictionary<string, int> counts = new() { [".ogg"] = 2, [".aac"] = 2 };

        // Act
        UnsupportedAudioSummary.TryGetDominant(counts, out string extension, out _);

        // Assert
        Assert.Equal(".aac", extension);
    }

    [Fact(DisplayName = "dominant_should_return_false_when_counts_are_empty")]
    public void TryGetDominant_ReturnsFalse_WhenCountsAreEmpty()
    {
        // Act
        bool found = UnsupportedAudioSummary.TryGetDominant(new Dictionary<string, int>(), out _, out _);

        // Assert
        Assert.False(found);
    }

    [Fact(DisplayName = "banner_should_cite_extension_and_count_when_unsupported_files_found")]
    public void BuildBannerMessage_CitesExtensionAndCount_WhenUnsupportedFilesFound()
    {
        // Arrange
        Dictionary<string, int> counts = new() { [".m4a"] = 312, [".wma"] = 4 };

        // Act
        string message = UnsupportedAudioSummary.BuildBannerMessage(PluralTemplate, SingularTemplate, Fallback, counts);

        // Assert
        Assert.Equal("312 .m4a files found", message);
    }

    [Fact(DisplayName = "banner_should_use_singular_template_when_single_file")]
    public void BuildBannerMessage_UsesSingularTemplate_WhenSingleFile()
    {
        // Arrange
        Dictionary<string, int> counts = new() { [".wav"] = 1 };

        // Act
        string message = UnsupportedAudioSummary.BuildBannerMessage(PluralTemplate, SingularTemplate, Fallback, counts);

        // Assert
        Assert.Equal("1 .wav file found", message);
    }

    [Fact(DisplayName = "banner_should_use_fallback_message_when_no_unsupported_files")]
    public void BuildBannerMessage_UsesFallbackMessage_WhenNoUnsupportedFiles()
    {
        // Act
        string message = UnsupportedAudioSummary.BuildBannerMessage(PluralTemplate, SingularTemplate, Fallback, new Dictionary<string, int>());

        // Assert
        Assert.Equal(Fallback, message);
    }

    [Fact(DisplayName = "merge_should_sum_counts_across_folders")]
    public void Merge_SumsCountsAcrossFolders()
    {
        // Arrange
        IReadOnlyDictionary<string, int>[] folders =
        [
            new Dictionary<string, int> { [".m4a"] = 10, [".ogg"] = 1 },
            new Dictionary<string, int> { [".m4a"] = 5 },
        ];

        // Act
        IReadOnlyDictionary<string, int> merged = UnsupportedAudioSummary.Merge(folders);

        // Assert
        Assert.Equal(15, merged[".m4a"]);
        Assert.Equal(1, merged[".ogg"]);
    }
}