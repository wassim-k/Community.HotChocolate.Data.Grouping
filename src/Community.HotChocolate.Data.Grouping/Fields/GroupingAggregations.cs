namespace HotChocolate.Data.Grouping.Fields;

/// <summary>
/// Aggregate operation(s). Used singly to identify a specific operation (e.g.
/// <see cref="Avg"/> on an <c>AggregateRequest</c>) or combined via bitwise OR
/// to express a set (e.g. <c>GroupingAggregations.Min | GroupingAggregations.Max</c> on
/// <c>IGroupingConventionDescriptor.AllowedAggregations</c>). <c>count</c> is
/// always emitted per bucket and is not represented here.
/// </summary>
[Flags]
public enum GroupingAggregations
{
    /// <summary>No aggregates.</summary>
    None = 0,
    /// <summary>Arithmetic mean.</summary>
    Avg = 1 << 0,
    /// <summary>Sum.</summary>
    Sum = 1 << 1,
    /// <summary>Minimum.</summary>
    Min = 1 << 2,
    /// <summary>Maximum.</summary>
    Max = 1 << 3,
    /// <summary>Every aggregate — the default for <c>AllowedAggregations</c>.</summary>
    All = Avg | Sum | Min | Max,
}
