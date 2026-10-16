using Microsoft.UI.Xaml.Controls;

namespace Rok.Dialogs;

public sealed partial class EditArtistDialog : ContentDialog
{
    /// <summary>Gets or sets the typed name.</summary>
    public string NameText { get; set; } = string.Empty;

    /// <summary>Gets or sets how the name field behaves. It must be set before the dialog is shown.</summary>
    public ArtistNameMode NameMode { get; set; } = ArtistNameMode.Hidden;

    public Visibility NameVisibility => NameMode == ArtistNameMode.Hidden ? Visibility.Collapsed : Visibility.Visible;

    public bool IsNameEnabled => NameMode == ArtistNameMode.Editable;

    public string? MusicBrainzID { get; set; }

    public string? FormedYear { get; set; }

    public string? BornYear { get; set; }

    public string? DiedYear { get; set; }

    public bool Disbanded { get; set; }

    public bool IsLock { get; set; }

    public string? Members { get; set; }

    public string? Biography { get; set; }


    public EditArtistDialog()
    {
        this.InitializeComponent();
    }

    /// <summary>Shows the reason the name is refused under the name field.</summary>
    /// <param name="message">The translated message.</param>
    public void ShowNameError(string message)
    {
        NameInput.Description = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["SystemFillColorCriticalBrush"]
        };
    }

    /// <summary>Disables the save button while the name is being checked.</summary>
    /// <param name="isBusy">Whether the check is running.</param>
    public void SetBusy(bool isBusy)
    {
        IsPrimaryButtonEnabled = !isBusy;
    }

    private void NameInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        NameInput.Description = null;
    }
}