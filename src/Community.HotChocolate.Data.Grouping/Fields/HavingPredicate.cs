using System.ComponentModel;
using HotChocolate.Data.Filters;
using HotChocolate.Language;

namespace HotChocolate.Data.Grouping.Fields;

/// <summary>
/// A parsed <c>having:</c> clause from a single aggregate operation.
/// </summary>
/// <param name="AggregateIndex">
/// Index into <see cref="SelectionPlan.Aggregates"/>, or <see langword="null"/> when the predicate
/// gates the bucket-level <c>count</c>.
/// </param>
/// <param name="FilterValue">The raw <c>having: { ... }</c> object literal from the query.</param>
/// <param name="FilterType">
/// The HotChocolate.Data <see cref="IFilterInputType"/> the visitor drives over <see cref="FilterValue"/>.
/// </param>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed record HavingPredicate(
    int? AggregateIndex,
    IValueNode FilterValue,
    IFilterInputType FilterType);
