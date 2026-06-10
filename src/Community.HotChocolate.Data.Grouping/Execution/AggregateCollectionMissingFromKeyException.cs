using HotChocolate.Data.Grouping.Fields;

namespace HotChocolate.Data.Grouping.Execution;

internal sealed class AggregateCollectionMissingFromKeyException(
    GroupingAggregations kind,
    PathSegment[] aggregatePath) : Exception
{
    public GroupingAggregations Kind { get; } = kind;

    public PathSegment[] AggregatePath { get; } = aggregatePath;
}
