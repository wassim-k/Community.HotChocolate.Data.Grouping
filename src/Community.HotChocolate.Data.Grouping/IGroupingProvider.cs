using HotChocolate.Data.Grouping.Fields;
using HotChocolate.Resolvers;

namespace HotChocolate.Data.Grouping;

/// <summary>
/// Translates a grouping selection into a <c>GROUP BY</c> and materialises bucket results.
/// </summary>
/// <remarks>Implementations return <see langword="null"/> for source shapes they don't recognise.</remarks>
public interface IGroupingProvider
{
    ValueTask<IReadOnlyList<GroupingResult<T>>?> ApplyAsync<T>(
        object? source,
        SelectionPlan plan,
        bool filterNullParent,
        IReadOnlyList<HavingPredicate> having,
        IMiddlewareContext context,
        CancellationToken cancellationToken);
}
