using System.Linq.Expressions;
using System.Reflection;
using HotChocolate.Data.Grouping.Convention;
using HotChocolate.Data.Grouping.Expressions;
using HotChocolate.Data.Grouping.Fields;
using static HotChocolate.Data.Grouping.Expressions.ExpressionUtilities;

namespace HotChocolate.Data.Grouping.Execution.QueryableGrouping;

internal sealed record CollectionPrefix(
    PathSegment[] Prefix,
    Type ElementType,
    PathSegment[]? Parent,
    bool IsPrimitive);

internal static class GroupingHelpers
{
    // Recurses through non-collection descendants looking for a scalar leaf matching predicate.
    public static bool HasLeaf(
        Type type,
        IGroupingConvention convention,
        Func<Type, bool> predicate,
        HashSet<Type>? visited = null)
    {
        visited ??= [];

        if (!visited.Add(type))
        {
            return false;
        }

        var unwrapped = Nullable.GetUnderlyingType(type) ?? type;

        if (convention.IsScalar(unwrapped))
        {
            return predicate(unwrapped);
        }

        // Collection-of-leaf is itself a leaf (e.g. int[]); defer to the element type.
        if (TryGetCollectionElementType(unwrapped, out var elementType)
            && convention.IsScalar(elementType))
        {
            return predicate(elementType);
        }

        foreach (var property in unwrapped.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var propertyType = property.PropertyType;
            if (TryGetCollectionElementType(propertyType, out var propertyElementType)
                && !convention.IsScalar(propertyElementType))
            {
                if (HasLeaf(propertyElementType, convention, predicate, visited))
                {
                    return true;
                }

                continue;
            }

            if (HasLeaf(propertyType, convention, predicate, visited))
            {
                return true;
            }
        }

        return false;
    }

    public static bool StartsWithPrefix(PathSegment[] path, PathSegment[] prefix) =>
        path.Length >= prefix.Length
        && path.Take(prefix.Length).SequenceEqual(prefix);

    // Returns one prefix per distinct collection hop, shortest-first so parents precede children.
    public static IReadOnlyList<CollectionPrefix> ResolveCollectionPrefixes(
        IEnumerable<PathSegment[]> paths)
    {
        var prefixes = new Dictionary<string, CollectionPrefix>(StringComparer.Ordinal);

        foreach (var path in paths)
        {
            PathSegment[]? parentPrefix = null;
            for (var i = 0; i < path.Length; i++)
            {
                if (path[i].Kind is not (PathSegmentKind.ObjectCollection or PathSegmentKind.PrimitiveCollection))
                {
                    continue;
                }

                var prefix = path[..(i + 1)];
                var key = StringifyPrefix(prefix);

                if (!prefixes.ContainsKey(key))
                {
                    var elementType = GetCollectionElementType(path[i].Property.PropertyType);
                    prefixes[key] = new CollectionPrefix(
                        prefix,
                        elementType,
                        parentPrefix,
                        path[i].Kind == PathSegmentKind.PrimitiveCollection);
                }

                if (path[i].Kind == PathSegmentKind.ObjectCollection)
                {
                    parentPrefix = prefix;
                }
            }
        }

        return [.. prefixes.Values.OrderBy(p => p.Prefix.Length)];
    }

    public static string StringifyPrefix(PathSegment[] prefix) =>
        string.Join('/', prefix.Select(s => s.Property.DeclaringType?.FullName + "." + s.Property.Name));

    // Raw Expression.Property access (no AsEnumerable wrappers — Mongo can't translate them).
    // In-memory mode coalesces nullable collections to Enumerable.Empty<> so SelectMany doesn't throw.
    public static Expression BuildCollectionPrefixAccess(
        Expression root,
        PathSegment[] prefix,
        bool inMemory,
        Type elementType)
    {
        var current = root;
        for (var i = 0; i < prefix.Length; i++)
        {
            var segment = prefix[i];
            var isLast = i == prefix.Length - 1;
            Expression next = Expression.Property(current, segment.Property);

            if (inMemory && !IsNonNullProperty(segment.Property))
            {
                if (isLast)
                {
                    var empty = Expression.Call(typeof(Enumerable), nameof(Enumerable.Empty), [elementType]);
                    next = Expression.Coalesce(next, empty);
                }
                else
                {
                    var empty = Expression.Call(typeof(Enumerable), nameof(Enumerable.Empty), [elementType]);
                    var remainder = BuildCollectionPrefixAccess(next, prefix[(i + 1)..], inMemory: true, elementType);
                    var notNull = Expression.NotEqual(next, Expression.Constant(null, next.Type));
                    return Expression.Condition(notNull, remainder, empty);
                }
            }

            current = next;
        }

        return current;
    }

    private static Type GetCollectionElementType(Type type) =>
        TryGetCollectionElementType(type, out var elementType)
            ? elementType
            : throw new InvalidOperationException(
                $"Segment classified as ObjectCollection but type {type} is not a collection.");
}
