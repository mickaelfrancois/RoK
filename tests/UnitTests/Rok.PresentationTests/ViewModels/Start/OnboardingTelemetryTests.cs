using Rok.ViewModels.Start;

namespace Rok.PresentationTests.ViewModels.Start;

public sealed class OnboardingTelemetryTests : IDisposable
{
    private readonly DirectoryInfo _tempDir = Directory.CreateTempSubdirectory("OnboardingTelemetryTests_");

    public void Dispose() => _tempDir.Delete(recursive: true);

    [Fact(DisplayName = "unsupported_format_properties_should_expose_fixed_extension_keys_and_total")]
    public void BuildUnsupportedFormatProperties_ExposesFixedExtensionKeysAndTotal()
    {
        // Arrange
        Dictionary<string, int> counts = new() { [".ogg"] = 312, [".opus"] = 4 };

        // Act
        Dictionary<string, object> properties = OnboardingTelemetry.BuildUnsupportedFormatProperties(counts);

        // Assert
        string[] expectedKeys = ["ape", "ogg", "opus", "wv", OnboardingTelemetry.UnsupportedTotalKey];
        Assert.Equal(expectedKeys.Order(), properties.Keys.Order());
        Assert.All(properties.Values, value => Assert.IsType<int>(value));
        Assert.Equal(312, properties["ogg"]);
        Assert.Equal(4, properties["opus"]);
        Assert.Equal(0, properties["ape"]);
        Assert.Equal(316, properties[OnboardingTelemetry.UnsupportedTotalKey]);
    }

    [Fact(DisplayName = "unsupported_format_properties_should_not_contain_any_path_or_file_name")]
    public async Task BuildUnsupportedFormatProperties_DoesNotContainAnyPathOrFileName()
    {
        // Arrange
        await File.WriteAllTextAsync(Path.Combine(_tempDir.FullName, "secret-name.ogg"), string.Empty);
        FolderScanResult scan = await FolderValidator.ScanAsync(_tempDir.FullName);

        // Act
        Dictionary<string, object> properties = OnboardingTelemetry.BuildUnsupportedFormatProperties(scan.UnsupportedCounts);

        // Assert
        IEnumerable<string> texts = properties.Keys.Concat(properties.Values.Select(value => value.ToString() ?? string.Empty));
        Assert.All(texts, text =>
        {
            Assert.DoesNotContain("secret", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\\", text, StringComparison.Ordinal);
            Assert.DoesNotContain(_tempDir.FullName, text, StringComparison.OrdinalIgnoreCase);
        });
        Assert.Equal(1, properties["ogg"]);
    }

    [Fact(DisplayName = "radio_fallback_properties_should_carry_reason_only")]
    public void BuildRadioFallbackProperties_CarriesReasonOnly()
    {
        // Act
        Dictionary<string, object> properties = OnboardingTelemetry.BuildRadioFallbackProperties("NoAudioFiles");

        // Assert
        Assert.Single(properties);
        Assert.Equal("NoAudioFiles", properties[OnboardingTelemetry.ReasonKey]);
    }
}