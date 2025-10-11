namespace Rok.Logic.Services;

public interface IDialogService
{
    Task ShowTextAsync(string title, string content, string closeButtonText);
}
