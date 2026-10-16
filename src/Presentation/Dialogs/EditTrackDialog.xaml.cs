using System.Globalization;
using Microsoft.UI.Xaml.Controls;

namespace Rok.Dialogs;

/// <summary>Dialog that edits the title, track number, disc, composers and BPM of a track and shows the live result of the change.</summary>
public sealed partial class EditTrackDialog : ContentDialog
{
    private readonly TrackEditValues _current;
    private readonly ResourceLoader _resourceLoader;
    private string _titleText;
    private string _trackNumberText;
    private string _discText;
    private string _composersText;
    private string _bpmText;
    private bool _isBusy;

    /// <summary>Gets or sets the typed title.</summary>
    public string TitleText
    {
        get => _titleText;
        set => Set(ref _titleText, value);
    }

    /// <summary>Gets or sets the typed track number.</summary>
    public string TrackNumberText
    {
        get => _trackNumberText;
        set => Set(ref _trackNumberText, value);
    }

    /// <summary>Gets or sets the typed disc number.</summary>
    public string DiscText
    {
        get => _discText;
        set => Set(ref _discText, value);
    }

    /// <summary>Gets or sets the typed composers.</summary>
    public string ComposersText
    {
        get => _composersText;
        set => Set(ref _composersText, value);
    }

    /// <summary>Gets or sets the typed beats per minute.</summary>
    public string BpmText
    {
        get => _bpmText;
        set => Set(ref _bpmText, value);
    }

    /// <summary>Gets what is typed in the dialog.</summary>
    public TrackEditInput Input => new(_titleText, _trackNumberText, _discText, _composersText, _bpmText);

    /// <summary>Creates the dialog seeded with the current values.</summary>
    /// <param name="current">The tag fields the track has now.</param>
    /// <param name="resourceLoader">Gives the validation messages.</param>
    public EditTrackDialog(TrackEditValues current, ResourceLoader resourceLoader)
    {
        _current = current;
        _resourceLoader = resourceLoader;
        _titleText = current.Title;
        _trackNumberText = FormatNumber(current.TrackNumber);
        _discText = FormatNumber(current.Disc);
        _composersText = current.Composers ?? string.Empty;
        _bpmText = FormatNumber(current.Bpm);

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

    private static string FormatNumber(int? value)
    {
        return value > 0 ? value.Value.ToString(CultureInfo.InvariantCulture) : string.Empty;
    }

    private void Set(ref string field, string value)
    {
        if (field == value)
            return;

        field = value;
        ErrorBar.IsOpen = false;
        Refresh();
    }

    private void Refresh()
    {
        TrackEditEvaluation evaluation = TrackEditRules.Evaluate(_current, Input);

        Describe(TitleInput, evaluation.Title);
        Describe(TrackNumberInput, evaluation.TrackNumber);
        Describe(DiscInput, evaluation.Disc);
        Describe(ComposersInput, evaluation.Composers);
        Describe(BpmInput, evaluation.Bpm);

        IsPrimaryButtonEnabled = !_isBusy && evaluation.CanSave;
    }

    private void Describe(TextBox box, TrackEditField field)
    {
        if (field.State == TrackEditFieldState.Invalid && field.ErrorKey is not null)
        {
            box.Description = new TextBlock
            {
                Text = _resourceLoader.GetString(field.ErrorKey),
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["SystemFillColorCriticalBrush"]
            };

            return;
        }

        box.Description = field.Preview;
    }
}