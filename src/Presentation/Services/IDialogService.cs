namespace Rok.Services;

public interface IDialogService
{
    Task ShowTextAsync(string title, string content, bool showTranslateButton = false, string targetLanguage = "fr");
}
