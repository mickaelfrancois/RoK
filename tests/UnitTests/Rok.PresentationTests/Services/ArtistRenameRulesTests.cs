using Rok.Application.Features.Artists.Rename;
using Rok.Services;

namespace Rok.PresentationTests.Services;

public class ArtistRenameRulesTests
{
    private static ArtistRenamePreview Preview(IReadOnlyList<ArtistRenameBlocker>? blockers = null, ArtistRenameCollision? collision = null)
        => new(1, "Old", "New", 3, 1, blockers ?? [], collision, false);

    [Theory(DisplayName = "the_name_field_follows_the_option_and_the_pending_marker")]
    [InlineData(false, null, ArtistNameMode.Hidden)]
    [InlineData(false, "Old", ArtistNameMode.Hidden)]
    [InlineData(true, null, ArtistNameMode.Editable)]
    [InlineData(true, "", ArtistNameMode.Editable)]
    [InlineData(true, "Old", ArtistNameMode.Locked)]
    public void NameMode_FollowsTheOptionAndTheMarker(bool writeTagsEnabled, string? pendingFrom, ArtistNameMode expected)
    {
        // Act
        ArtistNameMode mode = ArtistRenameRules.NameMode(writeTagsEnabled, pendingFrom);

        // Assert
        Assert.Equal(expected, mode);
    }

    [Theory(DisplayName = "a_typed_name_is_judged_against_the_current_name")]
    [InlineData("Old", "Old", ArtistNameChangeKind.Unchanged, "Old")]
    [InlineData("Old", "  Old  ", ArtistNameChangeKind.Unchanged, "Old")]
    [InlineData("Old", "old", ArtistNameChangeKind.Changed, "old")]
    [InlineData("Old", " New ", ArtistNameChangeKind.Changed, "New")]
    [InlineData("Old", "", ArtistNameChangeKind.Invalid, "")]
    [InlineData("Old", "   ", ArtistNameChangeKind.Invalid, "")]
    [InlineData("Old", null, ArtistNameChangeKind.Invalid, "")]
    public void Evaluate_JudgesTheTypedName(string current, string? typed, ArtistNameChangeKind expectedKind, string expectedName)
    {
        // Act
        ArtistNameChange change = ArtistRenameRules.Evaluate(current, typed);

        // Assert
        Assert.Equal(expectedKind, change.Kind);
        Assert.Equal(expectedName, change.Name);
    }

    [Fact(DisplayName = "a_name_of_255_characters_is_accepted_and_256_is_refused")]
    public void Evaluate_ChecksTheMaximumLength()
    {
        // Act
        ArtistNameChange accepted = ArtistRenameRules.Evaluate("Old", new string('a', ArtistRenameLimits.MaxNameLength));
        ArtistNameChange refused = ArtistRenameRules.Evaluate("Old", new string('a', ArtistRenameLimits.MaxNameLength + 1));

        // Assert
        Assert.Equal(ArtistNameChangeKind.Changed, accepted.Kind);
        Assert.Equal(ArtistNameChangeKind.Invalid, refused.Kind);
    }

    [Fact(DisplayName = "the_rename_can_start_only_without_blocker_and_without_collision")]
    public void CanStart_RequiresNoBlockerAndNoCollision()
    {
        // Arrange
        ArtistRenameBlocker blocker = new("a.mp3", EArtistRenameBlock.Locked, null);

        // Assert
        Assert.True(ArtistRenameRules.CanStart(Preview()));
        Assert.False(ArtistRenameRules.CanStart(Preview(blockers: [blocker])));
        Assert.False(ArtistRenameRules.CanStart(Preview(collision: new ArtistRenameCollision("New", 1, 2))));
    }
}