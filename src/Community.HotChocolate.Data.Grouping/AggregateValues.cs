using System.ComponentModel;

namespace HotChocolate.Data.Grouping;

/// <summary>
/// Operation slots for a single leaf's aggregates within a bucket. Absent ops stay <see langword="null"/>.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class AggregateValues
{
    /// <summary>Arithmetic mean, or <see langword="null"/> when <c>avg</c> wasn't selected.</summary>
    public object? Avg { get; init; }

    /// <summary>Sum, or <see langword="null"/> when <c>sum</c> wasn't selected.</summary>
    public object? Sum { get; init; }

    /// <summary>Minimum, or <see langword="null"/> when <c>min</c> wasn't selected.</summary>
    public object? Min { get; init; }

    /// <summary>Maximum, or <see langword="null"/> when <c>max</c> wasn't selected.</summary>
    public object? Max { get; init; }
}
