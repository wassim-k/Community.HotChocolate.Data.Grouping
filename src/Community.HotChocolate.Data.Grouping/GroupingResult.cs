using System.ComponentModel;

namespace HotChocolate.Data.Grouping;

/// <summary>
/// One grouping bucket.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class GroupingResult<T> : IGroupingResult
{
    /// <inheritdoc />
    public required GroupingFields Key { get; init; }

    /// <inheritdoc />
    public required int Count { get; init; }

    /// <inheritdoc />
    public required GroupingFields Aggregate { get; init; }
}
