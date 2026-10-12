using Microsoft.UI.Xaml.Controls;

namespace Rok.Dialogs;

/// <summary>Dialog that edits the title of a track and shows the live result of the change.</summary>
public sealed partial class EditTrackTitleDialog : ContentDialog
{
    private string _candidate = string.Empty;
    private bool _isBusy;

    /// <summary>Gets the title the track has now.</summary>
    public string CurrentTitle { get; }

    /// <summary>Gets or sets the typed title.</summary>
    public string Candidate
    {
        get => _candidate;
        set
        {
            if (_candidate == value)
                return;

            _candidate = value;
            ErrorBar.IsOpen = false;
            Refresh();
        }
    }

    /// <summary>Creates the dialog seeded with the current title.</summary>
    /// <param name="currentTitle">The title the track has now.</param>
    public EditTrackTitleDialog(string currentTitle)
    {
        CurrentTitle = currentTitle;
        _candidate = currentTitle;

        this.InitializeComponent();

        Refresh();
    }

    /// <summary>Shows a message and keeps the dialog open.</summary>
    /// <param name="message">The translated message.</param>
    public void ShowError(string message)
    {
        ErrorBar.Message = message;
        ErrorBar.IsOpen = true;
    }

    /// <summary>Disables the save button while a save is running.</summary>
    /// <param name="isBusy">Whether a save is running.</param>
    public void SetBusy(bool isBusy)
    {
        _isBusy = isBusy;
        Refresh();
    }

    private void Refresh()
    {
        PreviewText.Text = TrackTitleEditRules.FormatPreview(CurrentTitle, _candidate);
        IsPrimaryButtonEnabled = !_isBusy && TrackTitleEditRules.CanSave(CurrentTitle, _candidate);
    }
}