using HotChocolate.Data.Grouping.Convention;

namespace HotChocolate.Data.Grouping.Aggregates;

/// <summary>
/// Single source of truth for the built-in CLR-scalar → (<c>*AggregateResultType</c>, scalar kind)
/// mappings. The HAVING filter input type per aggregation operation is declared on each
/// <c>*AggregateResultType.Configure</c> via <c>.Having&lt;TRuntime&gt;()</c> — the grouping side names the
/// runtime CLR type, and HotChocolate.Data's filter convention resolves the matching
/// <c>*OperationFilterInputType</c> (including any consumer <c>BindRuntimeType</c> override) when
/// <c>AddFiltering()</c> is registered.
/// </summary>
internal static class DefaultGroupingBindings
{
    public readonly record struct Binding(
        Type Runtime,
        Type Result,
        GroupingScalarKind Kind);

    public static IReadOnlyList<Binding> All { get; } =
    [
        new(typeof(byte),               typeof(IntAggregateResultType),                 GroupingScalarKind.Numeric),
        new(typeof(sbyte),              typeof(IntAggregateResultType),                 GroupingScalarKind.Numeric),
        new(typeof(short),              typeof(IntAggregateResultType),                 GroupingScalarKind.Numeric),
        new(typeof(ushort),             typeof(IntAggregateResultType),                 GroupingScalarKind.Numeric),
        new(typeof(int),                typeof(IntAggregateResultType),                 GroupingScalarKind.Numeric),
        new(typeof(uint),               typeof(UIntAggregateResultType),                GroupingScalarKind.Numeric),
        new(typeof(long),               typeof(LongAggregateResultType),                GroupingScalarKind.Numeric),
        new(typeof(ulong),              typeof(LongAggregateResultType),                GroupingScalarKind.Numeric),
        new(typeof(decimal),            typeof(DecimalAggregateResultType),             GroupingScalarKind.Numeric),
        new(typeof(double),             typeof(FloatAggregateResultType),               GroupingScalarKind.Numeric),
        new(typeof(float),              typeof(FloatAggregateResultType),               GroupingScalarKind.Numeric),
        new(typeof(string),             typeof(StringAggregateResultType),              GroupingScalarKind.Comparable),
        new(typeof(bool),               typeof(BooleanAggregateResultType),             GroupingScalarKind.Comparable),
        new(typeof(Guid),               typeof(GuidAggregateResultType),                GroupingScalarKind.Comparable),
        new(typeof(DateTime),           typeof(DateTimeAggregateResultType),            GroupingScalarKind.Comparable),
        new(typeof(DateTimeOffset),     typeof(DateTimeOffsetAggregateResultType),      GroupingScalarKind.Comparable),
        new(typeof(DateOnly),           typeof(DateOnlyAggregateResultType),            GroupingScalarKind.Comparable),
        new(typeof(TimeOnly),           typeof(TimeOnlyAggregateResultType),            GroupingScalarKind.Comparable),
        new(typeof(TimeSpan),           typeof(TimeSpanAggregateResultType),            GroupingScalarKind.Comparable),
    ];
}
