using System.ComponentModel;

namespace HotChocolate.Data.Grouping.Fields;

/// <summary>
/// Parsed GraphQL grouping selection — key dimensions plus requested aggregate operations.
/// </summary>
/// <remarks>
/// HAVING predicates are not part of the plan — they're applied per-request on top of the cached pipeline.
/// An empty <c>KeyPaths</c> outer array means "group entire source into one bucket"; the middleware
/// substitutes a single empty path before handing the plan over.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed record SelectionPlan(
    PathSegment[][] KeyPaths,
    AggregateRequest[] Aggregates)
{
    public IEnumerable<PathSegment[]> AllPaths()
    {
        foreach (var path in KeyPaths)
        {
            yield return path;
        }
        foreach (var request in Aggregates)
        {
            yield return request.Path;
        }
    }
}
