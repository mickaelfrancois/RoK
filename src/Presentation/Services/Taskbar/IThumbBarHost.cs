namespace Rok.Services.Taskbar;

/// <summary>Shows the thumbnail toolbar buttons and reports their clicks.</summary>
public interface IThumbBarHost : IDisposable
{
    /// <summary>Raised when the user clicks one of the buttons.</summary>
    event EventHandler<ThumbBarButton>? ButtonClicked;

    /// <summary>Applies the state to the buttons. Must be called on the UI thread.</summary>
    void Apply(ThumbBarState state);
}