using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using HotChocolate.Types.Descriptors;

namespace HotChocolate.Data.Grouping.Expressions;

internal static class ExpressionUtilities
{
    private static readonly ConstantExpression _null = Expression.Constant(null, typeof(object));
    private static readonly DefaultTypeInspector _typeInspector = new();

    public static Expression NotNull(Expression expression) =>
        Expression.NotEqual(expression, _null);

    public static bool IsNonNullProperty(PropertyInfo property) =>
        new NullabilityInfoContext().Create(property).ReadState == NullabilityState.NotNull;

    public static bool IsNonNullType(Type type) =>
        type.IsValueType && Nullable.GetUnderlyingType(type) == null;

    public static bool TryGetCollectionElementType(
        Type type,
        [NotNullWhen(true)] out Type? elementType)
    {
        var extended = _typeInspector.GetType(type);
        if (extended.IsArrayOrList)
        {
            elementType = extended.ElementType.Type;
            return true;
        }

        elementType = null;
        return false;
    }

    public static bool IsCollection(Type type) =>
        TryGetCollectionElementType(type, out _);

    public static Expression ConvertToNullable(Expression expression) =>
        IsNonNullType(expression.Type)
            ? Expression.Convert(expression, typeof(Nullable<>).MakeGenericType(expression.Type))
            : expression;

    public static Expression CastAsEnumerable(this Expression expression) =>
        TryGetCollectionElementType(expression.Type, out var elementType) && expression.Type != typeof(IEnumerable<>).MakeGenericType(elementType)
            ? Expression.Call(
                typeof(Enumerable)
                    .GetMethod(nameof(Enumerable.AsEnumerable))!
                    .MakeGenericMethod(elementType),
                expression)
            : expression;

    public static Expression EmptyEnumerable(this Expression expression) =>
        TryGetCollectionElementType(expression.Type, out var elementType)
            ? Expression.Call(typeof(Enumerable), nameof(Enumerable.Empty), [elementType])
            : expression;
}
