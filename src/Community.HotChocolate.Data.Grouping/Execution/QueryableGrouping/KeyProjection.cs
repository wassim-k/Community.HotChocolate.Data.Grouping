using System.Linq.Expressions;
using HotChocolate.Data.Grouping.Fields;
using static HotChocolate.Data.Grouping.Expressions.ExpressionUtilities;

namespace HotChocolate.Data.Grouping.Execution.QueryableGrouping;

internal sealed class KeyProjection
{
    public Type Type { get; }
    public LambdaExpression Selector { get; }

    private KeyProjection(Type type, LambdaExpression selector)
    {
        Type = type;
        Selector = selector;
    }

    public static CarrierSlot[] Slots(Type keyType, int keyPathCount) =>
        AnonymousTypeUtils.ItemProperties(keyType, keyPathCount);

    public static KeyProjection Build(QueryShape shape, SelectionPlan plan)
    {
        var rowParameter = Expression.Parameter(shape.RowType, "src");

        // Group-all sentinel: project string "_" not 0 — MongoDB's $group reads `{ Item1: 0 }`
        // as an inclusion specification and rejects it; a string routes through literal handling.
        var slotExpressions = plan.KeyPaths
            .Select(path => path.Length == 0
                ? Expression.Constant("_")
                : ConvertToNullable(shape.RebasePath(path, rowParameter)))
            .ToList();

        var keyType = AnonymousTypeUtils.Create([.. slotExpressions.Select(e => e.Type)]);

        var construction = AnonymousTypeUtils.New(keyType, slotExpressions);
        var selector = Expression.Lambda(construction, rowParameter);

        return new KeyProjection(keyType, selector);
    }
}
