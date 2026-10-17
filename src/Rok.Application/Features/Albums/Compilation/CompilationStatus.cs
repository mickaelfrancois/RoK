using Rok.Domain.Enums;

namespace Rok.Application.Features.Albums.Compilation;

/// <summary>Compilation status of an album together with the reason for it.</summary>
/// <param name="IsCompilation">Whether the album is a compilation.</param>
/// <param name="Source">Origin of the status.</param>
public readonly record struct CompilationStatus(bool IsCompilation, ECompilationSource Source);