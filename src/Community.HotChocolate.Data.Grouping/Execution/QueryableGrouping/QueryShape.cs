using System.Linq.Expressions;
using System.Reflection;
using HotChocolate.Data.Grouping.Fields;
using static HotChocolate.Data.Grouping.Execution.QueryableGrouping.SafeNavigationBuilder;
using static HotChocolate.Data.Grouping.Expressions.ExpressionUtilities;

namespace HotChocolate.Data.Grouping.Execution.QueryableGrouping;

internal abstract record QueryShape(Expression Source, Type RowType)
{
    public abstract Expression RebasePath(PathSegment[] path, ParameterExpression rowParameter);

    // True when the parent path aligns with a SelectMany boundary and is guaranteed non-null by carrier construction.
    public abstract bool IsSelectManyBoundary(PathSegment[] parentPath);

    public static QueryShape Resolve(
        Type entityType,
        Expression source,
        SelectionPlan plan,
        bool filterNullParent,
        bool inMemory)
    {
        var paths = (PathSegment[][])[.. plan.AllPaths()];
        var prefixes = GroupingHelpers.ResolveCollectionPrefixes(plan.KeyPaths);

        QueryShape shape = prefixes.Count switch
        {
            0 => new Unflattened(source, entityType, inMemory),
            1 when paths.All(p => GroupingHelpers.StartsWithPrefix(p, prefixes[0].Prefix))
                => SinglePrefix.Build(source, entityType, prefixes[0], inMemory),
            _ => MultiPrefix.Build(source, entityType, prefixes, inMemory),
        };

        return filterNullParent
            ? shape with { Source = ApplyParentFilters(shape, plan.KeyPaths) }
            : shape;
    }

    private static Expression ApplyParentFilters(QueryShape shape, PathSegment[][] paths)
    {
        var rowParameter = Expression.Parameter(shape.RowType, "src");

        Expression? combined = null;
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var path in paths)
        {
            if (path.Length <= 1)
            {
                continue;
            }

            var parentPath = path[..^1];

            if (shape.IsSelectManyBoundary(parentPath))
            {
                continue;
            }

            if (!seen.Add(GroupingHelpers.StringifyPrefix(parentPath)))
            {
                continue;
            }

            var condition = NotNull(ConvertToNullable(shape.RebasePath(parentPath, rowParameter)));
            combined = combined is null ? condition : Expression.AndAlso(combined, condition);
        }

        if (combined is null)
        {
            return shape.Source;
        }

        var lambda = Expression.Lambda(combined, rowParameter);
        return Expression.Call(
            QueryableMethods.Where.MakeGenericMethod(shape.RowType),
            shape.Source,
            Expression.Quote(lambda));
    }

    private static PropertyInfo[] ToProperties(PathSegment[] path) =>
        [.. path.Select(s => s.Property)];

    private static LambdaExpression CollectionSelector(Expression body, ParameterExpression param, Type elementType) =>
        Expression.Lambda(
            typeof(Func<,>).MakeGenericType(param.Type, typeof(IEnumerable<>).MakeGenericType(elementType)),
            body,
            param);

    // No collection prefixes: GroupBy directly on T, no SelectMany.
    private sealed record Unflattened(Expression Source, Type EntityType, bool InMemory)
        : QueryShape(Source, EntityType)
    {
        public override Expression RebasePath(PathSegment[] path, ParameterExpression rowParameter) =>
            SafePropertyNavigation(rowParameter, ToProperties(path), InMemory);

        public override bool IsSelectManyBoundary(PathSegment[] parentPath) => false;
    }

    // One SelectMany over a single collection prefix that covers every requested path.
    private sealed record SinglePrefix(Expression Source, CollectionPrefix Prefix, bool InMemory)
        : QueryShape(Source, Prefix.ElementType)
    {
        public static SinglePrefix Build(Expression source, Type entityType, CollectionPrefix prefix, bool inMemory)
        {
            var src = Expression.Parameter(entityType, "src");
            var collection = GroupingHelpers.BuildCollectionPrefixAccess(src, prefix.Prefix, inMemory, prefix.ElementType);
            var flattened = Expression.Call(
                QueryableMethods.SelectMany.MakeGenericMethod(entityType, prefix.ElementType),
                source,
                Expression.Quote(CollectionSelector(collection, src, prefix.ElementType)));

            return new SinglePrefix(flattened, prefix, inMemory);
        }

        public override Expression RebasePath(PathSegment[] path, ParameterExpression rowParameter) =>
            Prefix.IsPrimitive && path.SequenceEqual(Prefix.Prefix)
                ? rowParameter
                : SafePropertyNavigation(rowParameter, ToProperties(path[Prefix.Prefix.Length..]), InMemory);

        public override bool IsSelectManyBoundary(PathSegment[] parentPath) =>
            GroupingHelpers.StartsWithPrefix(Prefix.Prefix, parentPath);
    }

    // Multi-collection: one SelectMany per prefix, growing the carrier by one slot per step.
    // Source stays at Item1; unwound elements occupy Item2..ItemN+1.
    private sealed record MultiPrefix(
        Expression Source,
        Type RowType,
        IReadOnlyList<CollectionPrefix> Prefixes,
        CarrierSlot[] CarrierSlots,
        bool InMemory) : QueryShape(Source, RowType)
    {
        public static MultiPrefix Build(
            Expression source,
            Type entityType,
            IReadOnlyList<CollectionPrefix> prefixes,
            bool inMemory)
        {
            var current = source;
            var rowType = entityType;
            CarrierSlot[]? slotProps = null;

            for (var step = 0; step < prefixes.Count; step++)
            {
                var prefix = prefixes[step];
                var prev = Expression.Parameter(rowType, step == 0 ? "src" : $"c{step}");
                var elem = Expression.Parameter(prefix.ElementType, $"e{step}");

                // Source-rooted reads from prev directly at step 0, else from prev.Item1; nested
                // reads from the parent element slot. Tail strips the parent prefix when present.
                var (collectionRoot, collectionTail) = (prefix.Parent, step) switch
                {
                    (null, 0) => (prev, prefix.Prefix),
                    (null, _) => (slotProps![0].Access(prev), prefix.Prefix),
                    _ => (slotProps![FindParentIndex(prefixes, prefix.Parent) + 1].Access(prev),
                          prefix.Prefix[prefix.Parent.Length..]),
                };

                var collection = GroupingHelpers.BuildCollectionPrefixAccess(collectionRoot, collectionTail, inMemory, prefix.ElementType);
                var collectionSelector = CollectionSelector(collection, prev, prefix.ElementType);

                Type[] nextTypes = [entityType, .. prefixes.Take(step + 1).Select(p => p.ElementType)];
                var nextType = AnonymousTypeUtils.Create(nextTypes);
                var nextProps = AnonymousTypeUtils.ItemProperties(nextType, nextTypes.Length);

                Expression[] values = step == 0
                    ? [prev, elem]
                    : [.. slotProps!.Select(p => p.Access(prev)), elem];

                var resultSelector = Expression.Lambda(AnonymousTypeUtils.New(nextType, values), prev, elem);

                current = Expression.Call(
                    QueryableMethods.SelectManyWithResult.MakeGenericMethod(rowType, prefix.ElementType, nextType),
                    current,
                    Expression.Quote(collectionSelector),
                    Expression.Quote(resultSelector));

                rowType = nextType;
                slotProps = nextProps;
            }

            return new MultiPrefix(current, rowType, prefixes, slotProps!, inMemory);
        }

        public override Expression RebasePath(PathSegment[] path, ParameterExpression rowParameter)
        {
            // Longest prefix first so a nested prefix wins over its parent.
            for (var i = Prefixes.Count - 1; i >= 0; i--)
            {
                var prefix = Prefixes[i].Prefix;
                if (GroupingHelpers.StartsWithPrefix(path, prefix))
                {
                    if (Prefixes[i].IsPrimitive && path.SequenceEqual(prefix))
                    {
                        return CarrierSlots[i + 1].Access(rowParameter);
                    }

                    return SafePropertyNavigation(
                        CarrierSlots[i + 1].Access(rowParameter),
                        ToProperties(path[prefix.Length..]),
                        InMemory);
                }
            }

            return SafePropertyNavigation(
                CarrierSlots[0].Access(rowParameter),
                ToProperties(path),
                InMemory);
        }

        public override bool IsSelectManyBoundary(PathSegment[] parentPath) =>
            Prefixes.Any(pr => GroupingHelpers.StartsWithPrefix(pr.Prefix, parentPath));

        private static int FindParentIndex(IReadOnlyList<CollectionPrefix> prefixes, PathSegment[] parent)
        {
            for (var i = 0; i < prefixes.Count; i++)
            {
                if (prefixes[i].Prefix.SequenceEqual(parent))
                {
                    return i;
                }
            }
            throw new InvalidOperationException("Parent prefix not found in prefix list.");
        }
    }
}
