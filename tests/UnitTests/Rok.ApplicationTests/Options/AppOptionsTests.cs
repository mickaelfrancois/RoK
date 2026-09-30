using System.Collections;
using System.Reflection;
using Rok.Application.Options;
using Rok.Application.Player;
using Rok.Application.Player.Output;

namespace Rok.ApplicationTests.Options;

public class AppOptionsTests
{
    [Fact(DisplayName = "copy_from_preserves_total_tracks_listened")]
    public void CopyFrom_PreservesTotalTracksListened()
    {
        // Arrange
        AppOptions source = new() { TotalTracksListened = 42 };
        AppOptions target = new();

        // Act
        target.CopyFrom(source);

        // Assert
        Assert.Equal(42, target.TotalTracksListened);
    }

    [Fact(DisplayName = "copy_from_preserves_replay_gain_options")]
    public void CopyFrom_PreservesReplayGainOptions()
    {
        // Arrange
        AppOptions source = new() { ReplayGainMode = EReplayGainMode.Track, ReplayGainPreampDb = 3.5 };
        AppOptions target = new();

        // Act
        target.CopyFrom(source);

        // Assert
        Assert.Equal(EReplayGainMode.Track, target.ReplayGainMode);
        Assert.Equal(3.5, target.ReplayGainPreampDb);
    }

    [Fact(DisplayName = "replay_gain_defaults_to_auto_without_preamp")]
    public void ReplayGain_DefaultsToAutoWithoutPreamp()
    {
        // Act
        AppOptions options = new();

        // Assert
        Assert.Equal(EReplayGainMode.Auto, options.ReplayGainMode);
        Assert.Equal(0, options.ReplayGainPreampDb);
    }

    [Fact(DisplayName = "output_defaults_to_windows_default_in_shared_mode")]
    public void Output_DefaultsToWindowsDefaultInSharedMode()
    {
        // Act
        AppOptions options = new();

        // Assert
        Assert.Equal(string.Empty, options.OutputDeviceId);
        Assert.Equal(EAudioOutputMode.Shared, options.OutputMode);
    }

    [Fact(DisplayName = "copy_from_preserves_review_fields")]
    public void CopyFrom_PreservesReviewFields()
    {
        // Arrange
        DateTimeOffset lastPrompt = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
        AppOptions source = new() { SessionsCount = 7, HasRated = true, ReviewLastPromptDate = lastPrompt };
        AppOptions target = new();

        // Act
        target.CopyFrom(source);

        // Assert
        Assert.Equal(7, target.SessionsCount);
        Assert.True(target.HasRated);
        Assert.Equal(lastPrompt, target.ReviewLastPromptDate);
    }

    [Fact(DisplayName = "copy_from_copies_every_interface_property")]
    public void CopyFrom_CopiesEveryInterfaceProperty()
    {
        // Arrange
        PropertyInfo[] properties = [.. typeof(IAppOptions).GetProperties().Where(property => property.CanRead && property.CanWrite)];
        Assert.NotEmpty(properties);

        AppOptions defaults = new();
        AppOptions source = new();

        foreach (PropertyInfo property in properties)
        {
            object? defaultValue = property.GetValue(defaults);
            object value = CreateNonDefaultValue(property, defaultValue);

            if (AreEqual(value, defaultValue))
                Assert.Fail($"Generated value for {property.Name} equals its default; fix the generator");

            property.SetValue(source, value);
        }

        AppOptions target = new();

        // Act
        target.CopyFrom(source);

        // Assert
        List<string> missing = [.. properties.Where(property => !AreEqual(property.GetValue(source), property.GetValue(target))).Select(property => property.Name)];
        Assert.True(missing.Count == 0, $"CopyFrom does not copy: {string.Join(", ", missing)}");
    }

    private static object CreateNonDefaultValue(PropertyInfo property, object? defaultValue)
    {
        Type type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

        if (type == typeof(bool))
            return !(bool)(defaultValue ?? false);

        if (type == typeof(int))
            return (int)(defaultValue ?? 0) + 1;

        if (type == typeof(double))
            return (double)(defaultValue ?? 0d) + 1.5;

        if (type == typeof(string))
            return $"{property.Name}-value";

        if (type == typeof(Guid))
            return Guid.NewGuid();

        if (type == typeof(DateTimeOffset))
            return new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

        if (type.IsEnum)
        {
            foreach (object candidate in Enum.GetValues(type))
            {
                if (!candidate.Equals(defaultValue))
                    return candidate;
            }

            Assert.Fail($"Enum {type.Name} of {property.Name} has no non-default value");
        }

        if (type == typeof(List<string>))
            return new List<string> { $"{property.Name}-item" };

        if (type == typeof(List<long>))
            return new List<long> { 4242 };

        Assert.Fail($"No non-default value for {property.Name} ({type.Name}); extend the generator");
        return null!;
    }

    private static bool AreEqual(object? left, object? right)
    {
        if (left is IEnumerable leftItems and not string && right is IEnumerable rightItems and not string)
            return leftItems.Cast<object>().SequenceEqual(rightItems.Cast<object>());

        return Equals(left, right);
    }
}