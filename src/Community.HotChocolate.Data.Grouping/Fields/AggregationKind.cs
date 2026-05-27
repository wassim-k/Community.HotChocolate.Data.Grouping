using System.ComponentModel;

namespace HotChocolate.Data.Grouping.Fields;

/// <summary>
/// The aggregate operation requested for a leaf path. <c>Count</c> is always emitted
/// per bucket and isn't represented here.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public enum AggregationKind
{
    /// <summary>Arithmetic mean.</summary>
    Avg,
    /// <summary>Sum.</summary>
    Sum,
    /// <summary>Minimum.</summary>
    Min,
    /// <summary>Maximum.</summary>
    Max,
}
