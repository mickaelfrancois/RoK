using Microsoft.UI.Xaml.Controls;

namespace Rok.Dialogs;

public sealed partial class EditAlbumDialog : ContentDialog
{
    public bool IsLive { get; set; }

    public bool IsBestOf { get; set; }

    public bool IsCompilation { get; set; }


    public EditAlbumDialog()
    {
        this.InitializeComponent();
    }
}