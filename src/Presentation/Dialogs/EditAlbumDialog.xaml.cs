using Microsoft.UI.Xaml.Controls;

namespace Rok.Dialogs;

public sealed partial class EditAlbumDialog : ContentDialog
{
    private IReadOnlyList<string> _artistSuggestions = [];
    private IReadOnlyList<string> _genreSuggestions = [];

    public bool IsLive { get; set; }

    public bool IsBestOf { get; set; }

    public bool IsCompilation { get; set; }

    public string? MusicBrainzID { get; set; }

    public string? ReleaseGroupMusicBrainzId { get; set; }

    public bool IsLock { get; set; }

    public string? LastFmUrl { get; set; }

    public string? Biography { get; set; }

    /// <summary>Gets or sets the typed album name.</summary>
    public string NameText
    {
        get => NameInput.Text;
        set => NameInput.Text = value;
    }

    /// <summary>Gets or sets the typed artist name.</summary>
    public string ArtistText
    {
        get => ArtistInput.Text;
        set => ArtistInput.Text = value;
    }

    /// <summary>Gets or sets the typed year.</summary>
    public string YearText
    {
        get => YearInput.Text;
        set => YearInput.Text = value;
    }

    /// <summary>Gets or sets the typed genre name.</summary>
    public string GenreText
    {
        get => GenreInput.Text;
        set => GenreInput.Text = value;
    }


    public EditAlbumDialog()
    {
        this.InitializeComponent();
    }

    /// <summary>Shows, locks or hides the fields that are written into the tags.</summary>
    /// <param name="mode">How the fields behave.</param>
    public void ApplyMode(AlbumTaggedFieldsMode mode)
    {
        TaggedPanel.Visibility = mode == AlbumTaggedFieldsMode.Hidden ? Visibility.Collapsed : Visibility.Visible;
        ArtistInput.IsEnabled = mode == AlbumTaggedFieldsMode.Editable;
    }

    /// <summary>Locks the genre field and explains why, when the import gives each track the genre of its artist.</summary>
    /// <param name="locked">Whether the genre cannot be edited.</param>
    public void ApplyGenreLock(bool locked)
    {
        GenreInput.IsEnabled = !locked;
        GenreLockedNote.Visibility = locked ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Gives the names the artist and genre fields suggest.</summary>
    /// <param name="suggestions">The names.</param>
    public void SetSuggestions(AlbumEditSuggestions suggestions)
    {
        _artistSuggestions = suggestions.Artists;
        _genreSuggestions = suggestions.Genres;
    }

    /// <summary>Shows the reason the typed values are refused.</summary>
    /// <param name="message">The translated message.</param>
    public void ShowError(string message)
    {
        ErrorBar.Message = message;
        ErrorBar.IsOpen = true;
    }

    /// <summary>Disables the save button while the typed values are being checked.</summary>
    /// <param name="isBusy">Whether the check is running.</param>
    public void SetBusy(bool isBusy)
    {
        IsPrimaryButtonEnabled = !isBusy;
    }

    private void Input_TextChanged(object sender, TextChangedEventArgs e)
    {
        ErrorBar.IsOpen = false;
    }

    private void ArtistInput_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        ErrorBar.IsOpen = false;
        Suggest(sender, args, _artistSuggestions);
    }

    private void GenreInput_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        ErrorBar.IsOpen = false;
        Suggest(sender, args, _genreSuggestions);
    }

    private static void Suggest(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args, IReadOnlyList<string> names)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
            return;

        string typed = sender.Text.Trim();

        sender.ItemsSource = typed.Length == 0
            ? null
            : names
                .Where(name => name.Contains(typed, StringComparison.CurrentCultureIgnoreCase))
                .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
                .Take(8)
                .ToList();
    }
}