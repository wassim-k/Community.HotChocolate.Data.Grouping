using System.Linq.Expressions;
using System.Reflection;
using static HotChocolate.Data.Grouping.Expressions.ExpressionUtilities;

namespace HotChocolate.Data.Grouping.Execution.QueryableGrouping;

internal static class SafeNavigationBuilder
{
    public static Expression SafePropertyNavigation(
        Expression body,
        ICollection<PropertyInfo> properties,
        bool inMemory = false) =>
        SafePropertyNavigation(body, properties as PropertyInfo[] ?? [.. properties], 0, inMemory);

    private static Expression SafePropertyNavigation(
        Expression body,
        PropertyInfo[] properties,
        int offset,
        bool inMemory)
    {
        if (offset >= properties.Length)
        {
            return body;
        }

        var property = properties[offset];

        return IsCollection(body.Type)
            ? HandleEnumerableNavigation(body, property, properties, offset + 1, inMemory)
            : HandleScalarNavigation(body, property, properties, offset + 1, inMemory);
    }

    private static Expression HandleEnumerableNavigation(
        Expression body,
        PropertyInfo property,
        PropertyInfo[] properties,
        int nextOffset,
        bool inMemory)
    {
        var elementType = body.Type.GetGenericArguments()[0];
        var parameter = Expression.Parameter(elementType, "item");
        var propertyAccess = Expression.Property(parameter, property);
        var innerExpression = SafePropertyNavigation(propertyAccess, properties, nextOffset, inMemory);
        var lambda = Expression.Lambda(innerExpression, parameter);

        var innerIsCollection = TryGetCollectionElementType(innerExpression.Type, out var innerElementType);
        var methodName = innerIsCollection ? nameof(Enumerable.SelectMany) : nameof(Enumerable.Select);
        var resultElementType = innerIsCollection ? innerElementType! : innerExpression.Type;

        return Expression.Call(
            typeof(Enumerable),
            methodName,
            [elementType, resultElementType],
            body,
            lambda);
    }

    private static Expression HandleScalarNavigation(
        Expression body,
        PropertyInfo property,
        PropertyInfo[] properties,
        int nextOffset,
        bool inMemory)
    {
        var propertyExpression = Expression.Property(body, property);
        var isLeaf = nextOffset >= properties.Length;

        if (isLeaf)
        {
            // Nullable enumerable in-memory: Coalesce to Enumerable.Empty<> so the
            // caller can iterate without a null check.
            if (inMemory && IsCollection(propertyExpression.Type) && !IsNonNullProperty(property))
            {
                return Expression.Condition(
                    NotNull(propertyExpression),
                    propertyExpression.CastAsEnumerable(),
                    propertyExpression.EmptyEnumerable());
            }

            return propertyExpression;
        }

        var innerExpression = SafePropertyNavigation(propertyExpression, properties, nextOffset, inMemory);

        if (IsNonNullProperty(property))
        {
            return innerExpression;
        }

        // LINQ providers handle null navigations server-side; only in-memory needs the
        // explicit guard. Enumerables coalesce to Empty<>; scalars project to nullable.
        if (IsCollection(innerExpression.Type))
        {
            return !inMemory
                ? innerExpression
                : Expression.Condition(
                    NotNull(propertyExpression),
                    innerExpression.CastAsEnumerable(),
                    innerExpression.EmptyEnumerable());
        }

        if (!inMemory)
        {
            return innerExpression;
        }

        var nullableExpression = ConvertToNullable(innerExpression);
        return Expression.Condition(
            NotNull(propertyExpression),
            nullableExpression,
            Expression.Constant(null, nullableExpression.Type));
    }
}
