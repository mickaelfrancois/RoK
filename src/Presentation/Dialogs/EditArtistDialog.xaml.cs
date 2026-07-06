using Microsoft.UI.Xaml.Controls;

namespace Rok.Dialogs;

public sealed partial class EditArtistDialog : ContentDialog
{
    public string? MusicBrainzID { get; set; }

    public string? FormedYear { get; set; }

    public string? BornYear { get; set; }

    public string? DiedYear { get; set; }

    public bool Disbanded { get; set; }

    public string? Members { get; set; }

    public string? Biography { get; set; }


    public EditArtistDialog()
    {
        this.InitializeComponent();
    }
}