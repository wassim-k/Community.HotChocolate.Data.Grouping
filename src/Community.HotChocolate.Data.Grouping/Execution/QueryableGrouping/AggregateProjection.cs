using System.Linq.Expressions;
using System.Reflection;
using HotChocolate.Data.Grouping.Aggregates;
using HotChocolate.Data.Grouping.Fields;
using static HotChocolate.Data.Grouping.Expressions.ExpressionUtilities;

namespace HotChocolate.Data.Grouping.Execution.QueryableGrouping;

internal static class AggregateProjection
{
    private static readonly MethodInfo _avgDecimalDefinition = FindAggregate(nameof(Enumerable.Average), typeof(decimal?));
    private static readonly MethodInfo _avgDoubleDefinition = FindAggregate(nameof(Enumerable.Average), typeof(double?));
    private static readonly MethodInfo _sumDecimalDefinition = FindAggregate(nameof(Enumerable.Sum), typeof(decimal?));
    private static readonly MethodInfo _sumDoubleDefinition = FindAggregate(nameof(Enumerable.Sum), typeof(double?));
    private static readonly MethodInfo _sumLongDefinition = FindAggregate(nameof(Enumerable.Sum), typeof(long?));
    private static readonly MethodInfo _minDefinition = FindMinMaxDefinition(nameof(Enumerable.Min));
    private static readonly MethodInfo _maxDefinition = FindMinMaxDefinition(nameof(Enumerable.Max));

    public static Expression Build(
        AggregateRequest request,
        ParameterExpression groupingParameter,
        Type rowType,
        Func<PathSegment[], ParameterExpression, Expression> rebase)
    {
        var elementParameter = Expression.Parameter(rowType, "e");
        var rebased = rebase(request.Path, elementParameter);

        // The leaf must rebase to a scalar. A collection-typed result means the path walks a
        // collection hop the key never flattened, so RebasePath couldn't strip it.
        if (IsCollection(rebased.Type))
        {
            throw new AggregateCollectionMissingFromKeyException(request.Kind, request.Path);
        }

        var target = AggregateWidening.Resolve(rebased.Type, request.Kind);

        var selectorBody = rebased.Type == target
            ? rebased
            : Expression.Convert(rebased, target);

        var selectorLambda = Expression.Lambda(selectorBody, elementParameter);

        var method = SelectMethod(request.Kind, target, rowType);
        return Expression.Call(method, groupingParameter, selectorLambda);
    }

    private static MethodInfo SelectMethod(GroupingAggregations kind, Type target, Type rowType) => kind switch
    {
        GroupingAggregations.Avg when target == typeof(decimal?) => _avgDecimalDefinition.MakeGenericMethod(rowType),
        GroupingAggregations.Avg => _avgDoubleDefinition.MakeGenericMethod(rowType),
        GroupingAggregations.Sum when target == typeof(decimal?) => _sumDecimalDefinition.MakeGenericMethod(rowType),
        GroupingAggregations.Sum when target == typeof(double?) => _sumDoubleDefinition.MakeGenericMethod(rowType),
        GroupingAggregations.Sum when target == typeof(long?) => _sumLongDefinition.MakeGenericMethod(rowType),
        GroupingAggregations.Min => _minDefinition.MakeGenericMethod(rowType, target),
        GroupingAggregations.Max => _maxDefinition.MakeGenericMethod(rowType, target),
        _ => throw ThrowHelper.Grouping_UnknownGroupingAggregations(),
    };

    private static MethodInfo FindAggregate(string name, Type nullableTarget) =>
        typeof(Enumerable).GetMethods().Single(m => m.Name == name
            && m.IsGenericMethodDefinition
            && m.GetParameters() is { Length: 2 } parameters
            && parameters[1].ParameterType.IsGenericType
            && parameters[1].ParameterType.GetGenericTypeDefinition() == typeof(Func<,>)
            && parameters[1].ParameterType.GetGenericArguments()[1] == nullableTarget);

    private static MethodInfo FindMinMaxDefinition(string name) =>
        typeof(Enumerable).GetMethods().Single(m => m.Name == name
            && m.IsGenericMethodDefinition
            && m.GetGenericArguments().Length == 2
            && m.GetParameters().Length == 2);
}
