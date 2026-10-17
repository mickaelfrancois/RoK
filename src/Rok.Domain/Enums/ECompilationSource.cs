namespace Rok.Domain.Enums;

/// <summary>Tells why an album is, or is not, a compilation. Only <see cref="Derived"/> is recomputed on import.</summary>
public enum ECompilationSource
{
    /// <summary>Deduced from the number of distinct artists in the folder.</summary>
    Derived = 0,

    /// <summary>Read from the compilation flag of the files (TCMP, COMPILATION, cpil).</summary>
    Tag = 1,

    /// <summary>Chosen by the user.</summary>
    Manual = 2,
}