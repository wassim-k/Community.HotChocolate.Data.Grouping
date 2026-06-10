using System.ComponentModel;

namespace HotChocolate.Data.Grouping;

/// <summary>
/// Type-erased view over <see cref="GroupingResult{T}"/> for the schema resolvers.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IGroupingResult
{
    GroupingFields Key { get; }

    int Count { get; }

    GroupingFields Aggregate { get; }
}
