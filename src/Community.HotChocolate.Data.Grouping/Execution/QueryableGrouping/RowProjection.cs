using System.Linq.Expressions;
using HotChocolate.Data.Grouping.Fields;

namespace HotChocolate.Data.Grouping.Execution.QueryableGrouping;

// Outer result-row layout: { Key, Count, Agg0..AggN-1 } as positional Item1..ItemN slots.
// Build below is the single producer of this ordering; the slot indices and count are exposed
// here so the consumers (ApplyHaving, Materialise) read the same layout from its author.
internal sealed class RowProjection
{
    public const int KeyIndex = 0;
    public const int CountIndex = 1;

    // Fixed leading slots (Key, Count) that precede the aggregate slots.
    public const int FixedSlots = 2;

    public Type Type { get; }
    public Type GroupingType { get; }
    public LambdaExpression Selector { get; }

    private RowProjection(Type type, Type groupingType, LambdaExpression selector)
    {
        Type = type;
        GroupingType = groupingType;
        Selector = selector;
    }

    public static int AggregateIndex(int ordinal) => FixedSlots + ordinal;

    public static int SlotCount(int aggregateCount) => FixedSlots + aggregateCount;

    public static CarrierSlot[] Slots(Type carrierType, int aggregateCount) =>
        AnonymousTypeUtils.ItemProperties(carrierType, SlotCount(aggregateCount));

    public static RowProjection Build(QueryShape shape, KeyProjection key, SelectionPlan plan)
    {
        var groupingType = typeof(IGrouping<,>).MakeGenericType(key.Type, shape.RowType);
        var groupingParameter = Expression.Parameter(groupingType, "g");

        // Slot order defines the carrier layout: Key, Count, then aggregates.
        var slots = new List<Expression>(SlotCount(plan.Aggregates.Length))
        {
            Expression.Property(groupingParameter, nameof(IGrouping<,>.Key)),
            Expression.Call(typeof(Enumerable), nameof(Enumerable.Count), [shape.RowType], groupingParameter),
        };

        foreach (var request in plan.Aggregates)
        {
            slots.Add(AggregateProjection.Build(
                request,
                groupingParameter,
                shape.RowType,
                shape.RebasePath));
        }

        var rowType = AnonymousTypeUtils.Create([.. slots.Select(s => s.Type)]);
        var construction = AnonymousTypeUtils.New(rowType, slots);
        var selector = Expression.Lambda(construction, groupingParameter);

        return new RowProjection(rowType, groupingType, selector);
    }
}
