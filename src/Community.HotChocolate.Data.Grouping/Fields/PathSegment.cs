using System.ComponentModel;
using System.Reflection;

namespace HotChocolate.Data.Grouping.Fields;

/// <summary>
/// A single property hop in a parsed leaf path, tagged with its
/// <see cref="PathSegmentKind"/> classification.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed record PathSegment(PropertyInfo Property, PathSegmentKind Kind);
