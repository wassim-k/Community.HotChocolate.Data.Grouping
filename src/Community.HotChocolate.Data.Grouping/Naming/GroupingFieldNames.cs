using HotChocolate.Data.Grouping.Fields;

namespace HotChocolate.Data.Grouping.Naming;

// Field names exposed on the generated grouping schema types. Part of the public
// GraphQL surface — these cannot be renamed.
internal static class GroupingFieldNames
{
    public const string Key = "key";
    public const string Aggregate = "aggregate";
    public const string Count = "count";

    public static string For(AggregationKind kind) => kind switch
    {
        AggregationKind.Avg => "avg",
        AggregationKind.Sum => "sum",
        AggregationKind.Min => "min",
        AggregationKind.Max => "max",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    public static bool TryParse(string name, out AggregationKind kind)
    {
        switch (name)
        {
            case "avg": kind = AggregationKind.Avg; return true;
            case "sum": kind = AggregationKind.Sum; return true;
            case "min": kind = AggregationKind.Min; return true;
            case "max": kind = AggregationKind.Max; return true;
            default: kind = default; return false;
        }
    }
}
