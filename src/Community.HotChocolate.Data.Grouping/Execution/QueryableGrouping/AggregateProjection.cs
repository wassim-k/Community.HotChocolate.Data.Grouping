using System.Linq.Expressions;
using System.Reflection;
using HotChocolate.Data.Grouping.Aggregates;
using HotChocolate.Data.Grouping.Fields;

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
        Func<PathSegment[], ParameterExpression, Expression> navigate)
    {
        var elementParameter = Expression.Parameter(rowType, "e");
        var navigation = navigate(request.Path, elementParameter);

        var target = AggregateWidening.Resolve(navigation.Type, request.Kind);

        var selectorBody = navigation.Type == target
            ? navigation
            : Expression.Convert(navigation, target);

        var selectorLambda = Expression.Lambda(selectorBody, elementParameter);

        var method = SelectMethod(request.Kind, target, rowType);
        return Expression.Call(method, groupingParameter, selectorLambda);
    }

    private static MethodInfo SelectMethod(AggregationKind kind, Type target, Type rowType) => kind switch
    {
        AggregationKind.Avg when target == typeof(decimal?) => _avgDecimalDefinition.MakeGenericMethod(rowType),
        AggregationKind.Avg => _avgDoubleDefinition.MakeGenericMethod(rowType),
        AggregationKind.Sum when target == typeof(decimal?) => _sumDecimalDefinition.MakeGenericMethod(rowType),
        AggregationKind.Sum when target == typeof(double?) => _sumDoubleDefinition.MakeGenericMethod(rowType),
        AggregationKind.Sum when target == typeof(long?) => _sumLongDefinition.MakeGenericMethod(rowType),
        AggregationKind.Min => _minDefinition.MakeGenericMethod(rowType, target),
        AggregationKind.Max => _maxDefinition.MakeGenericMethod(rowType, target),
        _ => throw ThrowHelper.Grouping_UnknownAggregationKind(),
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
