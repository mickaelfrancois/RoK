namespace Rok.Application.Interfaces;

public interface IDominantColorCalculator
{
    Task<long?> CalculateAsync(string imagePath);
}