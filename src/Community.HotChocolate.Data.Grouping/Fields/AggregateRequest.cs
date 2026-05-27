using System.ComponentModel;

namespace HotChocolate.Data.Grouping.Fields;

/// <summary>
/// One requested aggregate — a leaf path plus the operation to perform on it.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed record AggregateRequest(PathSegment[] Path, AggregationKind Kind);
