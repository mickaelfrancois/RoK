namespace Rok.Application.Interfaces;

public interface ITranslateService
{
    bool IsEnable { get; set; }

    Task<string?> TranslateAsync(string text, string targetLang, CancellationToken cancellationToken = default);
}
