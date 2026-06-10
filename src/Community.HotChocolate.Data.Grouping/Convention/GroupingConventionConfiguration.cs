using System.ComponentModel;
using HotChocolate.Data.Grouping.Fields;

namespace HotChocolate.Data.Grouping.Convention;

/// <summary>
/// Configuration for a <see cref="GroupingConvention"/>. Public only so it can satisfy the
/// convention's descriptor surface; consumers configure through
/// <see cref="IGroupingConventionDescriptor"/> rather than touching this bag directly.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public class GroupingConventionConfiguration
{
    public string? Scope { get; set; }

    /// <summary>
    /// CLR-runtime-type → <see cref="GroupingScalarKind"/> registry — controls which types are
    /// recognised as scalar leaves and whether they are eligible for SUM/AVG (Numeric) vs. just
    /// MIN/MAX (Comparable). Keys are unwrapped (no <see cref="Nullable{T}"/>).
    /// </summary>
    public IDictionary<Type, GroupingScalarKind> ScalarKinds { get; } = new Dictionary<Type, GroupingScalarKind>();

    /// <summary>
    /// CLR-runtime-type → <c>*AggregateResultType</c>. Drives the output-schema choice (which
    /// <c>*AggregateResult</c> type a leaf exposes). Keys are unwrapped.
    /// </summary>
    public IDictionary<Type, AggregateBinding> AggregateBindings { get; } = new Dictionary<Type, AggregateBinding>();

    /// <summary>
    /// Default value of the auto-generated <c>filterNullParent: Boolean</c> argument on every
    /// <c>groupBy*</c> field.
    /// </summary>
    public bool DefaultFilterNullParent { get; set; }

    /// <summary>
    /// Aggregate operations exposed on each <c>*AggregateResult</c> type. Defaults to
    /// <see cref="GroupingAggregations.All"/>. <see cref="GroupingAggregations.None"/> omits the
    /// <c>aggregate</c> field from <c>*Grouping</c> entirely.
    /// </summary>
    public GroupingAggregations AllowedAggregations { get; set; } = GroupingAggregations.All;

    /// <summary>
    /// Optional <see cref="IGroupingProvider"/> implementation type. Resolved at convention completion.
    /// </summary>
    public Type? GroupingProvider { get; set; }

    /// <summary>
    /// Pre-constructed <see cref="IGroupingProvider"/> instance. Takes precedence over <see cref="GroupingProvider"/>.
    /// </summary>
    public IGroupingProvider? GroupingProviderInstance { get; set; }
}
