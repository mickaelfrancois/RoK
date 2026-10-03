using Rok.ViewModels.Listening.Services;

namespace Rok.PresentationTests.ViewModels.Listening.Services;

public class ListeningHeaderLabelsTests
{
    private const string Add = "Ajouter {0} aux favoris";
    private const string Remove = "Retirer {0} des favoris";

    [Fact(DisplayName = "favorite_toggle_names_item_and_add_action")]
    public void FavoriteToggle_NamesItemAndAddAction()
    {
        // Act
        var result = ListeningHeaderLabels.FavoriteToggle("Muse", false, Add, Remove);

        // Assert
        Assert.Equal("Ajouter Muse aux favoris", result);
    }

    [Fact(DisplayName = "favorite_toggle_names_item_and_remove_action")]
    public void FavoriteToggle_NamesItemAndRemoveAction()
    {
        // Act
        var result = ListeningHeaderLabels.FavoriteToggle("Muse", true, Add, Remove);

        // Assert
        Assert.Equal("Retirer Muse des favoris", result);
    }

    [Fact(DisplayName = "sleep_button_shows_idle_label_when_inactive")]
    public void SleepButton_ShowsIdleLabel_WhenInactive()
    {
        // Act
        var result = ListeningHeaderLabels.SleepButton(false, 300, "Minuterie", "Arrêt dans {0} min");

        // Assert
        Assert.Equal("Minuterie", result);
    }

    [Theory(DisplayName = "sleep_button_rounds_remaining_minutes_up")]
    [InlineData(61, "2")]
    [InlineData(60, "1")]
    [InlineData(1, "1")]
    public void SleepButton_RoundsRemainingMinutesUp(int seconds, string expected)
    {
        // Act
        var result = ListeningHeaderLabels.SleepButton(true, seconds, "Minuterie", "{0}");

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact(DisplayName = "sleep_button_formats_remaining_with_localized_pattern")]
    public void SleepButton_FormatsRemainingWithLocalizedPattern()
    {
        // Act
        var result = ListeningHeaderLabels.SleepButton(true, 300, "Minuterie", "Arrêt dans {0} min");

        // Assert
        Assert.Equal("Arrêt dans 5 min", result);
    }
}