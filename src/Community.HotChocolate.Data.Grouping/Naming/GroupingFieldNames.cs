using HotChocolate.Data.Grouping.Fields;

namespace HotChocolate.Data.Grouping.Naming;

// Field names exposed on the generated grouping schema types. Part of the public
// GraphQL surface — these cannot be renamed.
internal static class GroupingFieldNames
{
    public const string Key = "key";
    public const string Aggregate = "aggregate";
    public const string Count = "count";

    public static string For(GroupingAggregations kind) => kind switch
    {
        GroupingAggregations.Avg => "avg",
        GroupingAggregations.Sum => "sum",
        GroupingAggregations.Min => "min",
        GroupingAggregations.Max => "max",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    public static bool TryParse(string name, out GroupingAggregations kind)
    {
        switch (name)
        {
            case "avg": kind = GroupingAggregations.Avg; return true;
            case "sum": kind = GroupingAggregations.Sum; return true;
            case "min": kind = GroupingAggregations.Min; return true;
            case "max": kind = GroupingAggregations.Max; return true;
            default: kind = default; return false;
        }
    }
}
