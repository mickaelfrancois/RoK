using Rok.Services;

namespace Rok.PresentationTests.Services;

public class TrackEditRulesTests
{
    private static readonly TrackEditValues Current = new(7, "Old", 3, 1, "Composer", 120);

    private static TrackEditInput Same()
        => new("Old", "3", "1", "Composer", "120");

    [Fact(DisplayName = "nothing_changed_cannot_be_saved")]
    public void Evaluate_NothingChanged_CannotSave()
    {
        // Act
        TrackEditEvaluation result = TrackEditRules.Evaluate(Current, Same());

        // Assert
        Assert.False(result.CanSave);
        Assert.Equal(TrackEditFieldState.Unchanged, result.Title.State);
        Assert.Equal(TrackEditFieldState.Unchanged, result.TrackNumber.State);
        Assert.Null(result.Edit.Title);
        Assert.Null(result.Edit.TrackNumber);
        Assert.Null(result.Edit.Disc);
        Assert.Null(result.Edit.Composers);
        Assert.Null(result.Edit.Bpm);
    }

    [Theory(DisplayName = "blank_title_is_invalid_and_required")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Evaluate_BlankTitle_IsInvalid(string? title)
    {
        // Act
        TrackEditEvaluation result = TrackEditRules.Evaluate(Current, Same() with { Title = title });

        // Assert
        Assert.Equal(TrackEditFieldState.Invalid, result.Title.State);
        Assert.Equal(TrackEditTextKeys.Required, result.Title.ErrorKey);
        Assert.False(result.CanSave);
    }

    [Theory(DisplayName = "title_with_only_spaces_around_is_unchanged")]
    [InlineData("  Old  ")]
    [InlineData("Old")]
    public void Evaluate_TitleWithSpaces_IsUnchanged(string title)
    {
        // Act
        TrackEditEvaluation result = TrackEditRules.Evaluate(Current, Same() with { Title = title });

        // Assert
        Assert.Equal(TrackEditFieldState.Unchanged, result.Title.State);
        Assert.False(result.CanSave);
    }

    [Fact(DisplayName = "changed_title_is_trimmed_and_previewed_old_then_new")]
    public void Evaluate_ChangedTitle_IsTrimmedAndPreviewed()
    {
        // Act
        TrackEditEvaluation result = TrackEditRules.Evaluate(Current, Same() with { Title = "  New " });

        // Assert
        Assert.True(result.CanSave);
        Assert.Equal("Old → New", result.Title.Preview);
        Assert.Equal("New", result.Edit.Title);
        Assert.Null(result.Edit.TrackNumber);
        Assert.Equal(7, result.Edit.TrackId);
    }

    [Theory(DisplayName = "emptied_track_number_or_disc_is_required_when_a_value_exists")]
    [InlineData("", "1")]
    [InlineData("3", "")]
    public void Evaluate_EmptiedNumber_IsRequired(string trackNumber, string disc)
    {
        // Act
        TrackEditEvaluation result = TrackEditRules.Evaluate(Current, Same() with { TrackNumber = trackNumber, Disc = disc });

        // Assert
        TrackEditField field = trackNumber.Length == 0 ? result.TrackNumber : result.Disc;
        Assert.Equal(TrackEditFieldState.Invalid, field.State);
        Assert.Equal(TrackEditTextKeys.Required, field.ErrorKey);
        Assert.False(result.CanSave);
    }

    [Theory(DisplayName = "track_number_out_of_bounds_or_not_a_number_is_invalid")]
    [InlineData("0")]
    [InlineData("abc")]
    [InlineData("1000")]
    [InlineData("-1")]
    [InlineData("1.5")]
    [InlineData("99999999999")]
    public void Evaluate_BadTrackNumber_IsInvalid(string text)
    {
        // Act
        TrackEditEvaluation result = TrackEditRules.Evaluate(Current, Same() with { TrackNumber = text });

        // Assert
        Assert.Equal(TrackEditFieldState.Invalid, result.TrackNumber.State);
        Assert.Equal(TrackEditTextKeys.InvalidTrackNumber, result.TrackNumber.ErrorKey);
        Assert.False(result.CanSave);
    }

    [Theory(DisplayName = "disc_out_of_bounds_is_invalid")]
    [InlineData("0")]
    [InlineData("100")]
    public void Evaluate_BadDisc_IsInvalid(string text)
    {
        // Act
        TrackEditEvaluation result = TrackEditRules.Evaluate(Current, Same() with { Disc = text });

        // Assert
        Assert.Equal(TrackEditFieldState.Invalid, result.Disc.State);
        Assert.Equal(TrackEditTextKeys.InvalidDisc, result.Disc.ErrorKey);
    }

    [Fact(DisplayName = "zero_padded_number_equal_to_the_current_one_is_unchanged")]
    public void Evaluate_ZeroPaddedNumber_IsUnchanged()
    {
        // Act
        TrackEditEvaluation result = TrackEditRules.Evaluate(Current, Same() with { TrackNumber = "03" });

        // Assert
        Assert.Equal(TrackEditFieldState.Unchanged, result.TrackNumber.State);
        Assert.False(result.CanSave);
    }

    [Fact(DisplayName = "changed_track_number_is_sent_and_previewed")]
    public void Evaluate_ChangedTrackNumber_IsSent()
    {
        // Act
        TrackEditEvaluation result = TrackEditRules.Evaluate(Current, Same() with { TrackNumber = "04" });

        // Assert
        Assert.True(result.CanSave);
        Assert.Equal(4, result.Edit.TrackNumber);
        Assert.Equal("3 → 4", result.TrackNumber.Preview);
    }

    [Fact(DisplayName = "current_zero_track_number_shows_as_empty_and_can_be_set")]
    public void Evaluate_CurrentZeroTrackNumber_CanBeSet()
    {
        // Arrange
        TrackEditValues current = Current with { TrackNumber = 0 };

        // Act
        TrackEditEvaluation unchanged = TrackEditRules.Evaluate(current, Same() with { TrackNumber = "" });
        TrackEditEvaluation set = TrackEditRules.Evaluate(current, Same() with { TrackNumber = "5" });

        // Assert
        Assert.Equal(TrackEditFieldState.Unchanged, unchanged.TrackNumber.State);
        Assert.Equal(TrackEditFieldState.Changed, set.TrackNumber.State);
        Assert.Equal("— → 5", set.TrackNumber.Preview);
    }

    [Fact(DisplayName = "emptied_bpm_removes_the_value")]
    public void Evaluate_EmptiedBpm_RemovesValue()
    {
        // Act
        TrackEditEvaluation result = TrackEditRules.Evaluate(Current, Same() with { Bpm = "" });

        // Assert
        Assert.True(result.CanSave);
        Assert.Equal(0, result.Edit.Bpm);
        Assert.Equal("120 → —", result.Bpm.Preview);
    }

    [Fact(DisplayName = "empty_bpm_without_a_current_value_is_unchanged")]
    public void Evaluate_EmptyBpmWithoutValue_IsUnchanged()
    {
        // Act
        TrackEditEvaluation result = TrackEditRules.Evaluate(Current with { Bpm = null }, Same() with { Bpm = "" });

        // Assert
        Assert.Equal(TrackEditFieldState.Unchanged, result.Bpm.State);
        Assert.Null(result.Edit.Bpm);
    }

    [Theory(DisplayName = "bpm_out_of_bounds_or_not_a_number_is_invalid")]
    [InlineData("0")]
    [InlineData("1000")]
    [InlineData("fast")]
    public void Evaluate_BadBpm_IsInvalid(string text)
    {
        // Act
        TrackEditEvaluation result = TrackEditRules.Evaluate(Current, Same() with { Bpm = text });

        // Assert
        Assert.Equal(TrackEditFieldState.Invalid, result.Bpm.State);
        Assert.Equal(TrackEditTextKeys.InvalidBpm, result.Bpm.ErrorKey);
    }

    [Fact(DisplayName = "composer_is_trimmed_and_emptied_composer_is_a_removal")]
    public void Evaluate_Composers_AreTrimmedAndRemovable()
    {
        // Act
        TrackEditEvaluation changed = TrackEditRules.Evaluate(Current, Same() with { Composers = "  Bach  " });
        TrackEditEvaluation removed = TrackEditRules.Evaluate(Current, Same() with { Composers = "   " });

        // Assert
        Assert.Equal("Bach", changed.Edit.Composers);
        Assert.Equal("Composer → Bach", changed.Composers.Preview);
        Assert.Equal(string.Empty, removed.Edit.Composers);
        Assert.True(removed.CanSave);
    }

    [Fact(DisplayName = "invalid_field_blocks_saving_even_when_another_field_changed")]
    public void Evaluate_InvalidFieldBlocksSave()
    {
        // Act
        TrackEditEvaluation result = TrackEditRules.Evaluate(Current, Same() with { Title = "New", Bpm = "abc" });

        // Assert
        Assert.False(result.CanSave);
    }

    [Fact(DisplayName = "only_changed_fields_are_sent")]
    public void Evaluate_OnlyChangedFieldsAreSent()
    {
        // Act
        TrackEditEvaluation result = TrackEditRules.Evaluate(Current, Same() with { Disc = "2", Bpm = "130" });

        // Assert
        Assert.Null(result.Edit.Title);
        Assert.Null(result.Edit.TrackNumber);
        Assert.Equal(2, result.Edit.Disc);
        Assert.Null(result.Edit.Composers);
        Assert.Equal(130, result.Edit.Bpm);
    }

    [Fact(DisplayName = "normalize_trims_the_text")]
    public void Normalize_TrimsText()
    {
        // Act
        string result = TrackEditRules.Normalize("  Hello  ");

        // Assert
        Assert.Equal("Hello", result);
    }
}