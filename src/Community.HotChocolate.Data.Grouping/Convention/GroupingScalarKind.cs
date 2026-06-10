namespace HotChocolate.Data.Grouping.Convention;

/// <summary>
/// Classifies a runtime CLR type's role within the grouping schema.
/// </summary>
/// <remarks>
/// <see cref="Numeric"/> is a strict subset of <see cref="Comparable"/>; AVG/SUM only emit for
/// numeric scalars while MIN/MAX accept any comparable.
/// </remarks>
public enum GroupingScalarKind
{
    /// <summary>Comparable scalar — eligible for keys and MIN/MAX.</summary>
    Comparable = 1,

    /// <summary>Numeric scalar — eligible for keys, MIN/MAX, AVG, and SUM.</summary>
    Numeric = 2,
}
