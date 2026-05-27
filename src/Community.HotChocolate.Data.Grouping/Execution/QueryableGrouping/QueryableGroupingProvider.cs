using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using HotChocolate.Data.Filters;
using HotChocolate.Data.Filters.Expressions;
using HotChocolate.Data.Grouping.Fields;
using HotChocolate.Resolvers;
using HotChocolate.Types.Descriptors;

namespace HotChocolate.Data.Grouping.Execution.QueryableGrouping;

/// <summary>
/// <see cref="IQueryable"/>-backed <see cref="IGroupingProvider"/>.
/// </summary>
public class QueryableGroupingProvider : IGroupingProvider
{
    private static readonly MethodInfo _groupByMethod = typeof(Queryable).GetMethods()
        .Single(m =>
            m.Name == nameof(Queryable.GroupBy)
            && m.GetParameters().Length == 2
            && m.GetGenericArguments().Length == 2);

    private static readonly MethodInfo _selectMethod = typeof(Queryable).GetMethods()
        .Single(m =>
            m.Name == nameof(Queryable.Select)
            && m.GetParameters().Length == 2
            && m.GetParameters()[1].ParameterType
                .GetGenericArguments()[0]
                .GetGenericArguments().Length == 2);

    private static readonly MethodInfo _whereMethod = typeof(Queryable).GetMethods()
        .First(m => m.Name == nameof(Queryable.Where) && m.GetParameters().Length == 2);

    private static readonly MethodInfo _executableFromMethod = typeof(Executable).GetMethods()
        .Single(m => m.Name == nameof(Executable.From)
            && m.IsGenericMethodDefinition
            && m.GetParameters() is { Length: 2 } p
            && p[0].ParameterType.IsGenericType
            && p[0].ParameterType.GetGenericTypeDefinition() == typeof(IQueryable<>));

    private static readonly FilterVisitor<QueryableFilterContext, Expression> _havingVisitor =
        new(new QueryableCombinator());

    private static readonly DefaultTypeInspector _typeInspector = new();

    /// <inheritdoc />
    public virtual async ValueTask<IReadOnlyList<GroupingResult<T>>?> ApplyAsync<T>(
        object? source,
        SelectionPlan plan,
        bool filterNullParent,
        IReadOnlyList<HavingPredicate> having,
        IMiddlewareContext? context,
        CancellationToken cancellationToken)
    {
        var (queryable, inMemory) = source switch
        {
            IQueryableExecutable<T> qe => (qe.Source, qe.IsInMemory),
            IQueryable<T> q => (q, q is EnumerableQuery),
            IEnumerable<T> e => (e.AsQueryable(), true),
            _ => default,
        };

        if (queryable is null)
        {
            return null;
        }

        var projected = BuildQuery(queryable, plan, filterNullParent, inMemory, having);
        var executable = WrapAsExecutable(source as IQueryableExecutable<T>, projected);
        var rows = await executable.ToListAsync(cancellationToken).ConfigureAwait(false);

        return [.. Materialise<T>(rows, projected.ElementType, plan)];
    }

    /// <summary>
    /// Builds the grouping query over <paramref name="source"/> and composes HAVING predicates on top.
    /// </summary>
    protected virtual IQueryable BuildQuery<T>(
        IQueryable<T> source,
        SelectionPlan plan,
        bool filterNullParent,
        bool inMemory,
        IReadOnlyList<HavingPredicate> having)
    {
        var shape = QueryShape.Resolve(typeof(T), source.Expression, plan, filterNullParent, inMemory);
        var keyProj = KeyProjection.Build(shape, plan);
        var rowProj = RowProjection.Build(shape, keyProj, plan);

        var grouped = Expression.Call(
            _groupByMethod.MakeGenericMethod(shape.RowType, keyProj.Type),
            shape.Source,
            Expression.Quote(keyProj.Selector));

        var projection = Expression.Call(
            _selectMethod.MakeGenericMethod(rowProj.GroupingType, rowProj.Type),
            grouped,
            Expression.Quote(rowProj.Selector));

        var projected = source.Provider.CreateQuery(projection);

        return ApplyHaving(projected, rowProj.Type, plan.Aggregates.Length, having, inMemory);
    }

    private static IExecutable WrapAsExecutable<T>(IQueryableExecutable<T>? original, IQueryable projected)
    {
        var rowType = projected.ElementType;

        if (original is not null)
        {
            var withSource = typeof(IQueryableExecutable<T>).GetMethods()
                .Single(m => m.Name == nameof(IQueryableExecutable<>.WithSource)
                    && m.IsGenericMethod
                    && m.GetParameters().Length == 1)
                .MakeGenericMethod(rowType);
            return (IExecutable)withSource.Invoke(original, [projected])!;
        }

        return (IExecutable)_executableFromMethod
            .MakeGenericMethod(rowType)
            .Invoke(null, [projected, null])!;
    }

    private static IQueryable ApplyHaving(
        IQueryable projected,
        Type carrierType,
        int aggregateCount,
        IReadOnlyList<HavingPredicate> clauses,
        bool inMemory)
    {
        if (clauses.Count == 0)
        {
            return projected;
        }

        var rowSlots = RowProjection.Slots(carrierType, aggregateCount);
        var rowParameter = Expression.Parameter(carrierType, "row");
        Expression? combined = null;

        foreach (var clause in clauses)
        {
            var slotIndex = clause.AggregateIndex is { } aggregateIndex
                ? RowProjection.AggregateIndex(aggregateIndex)
                : RowProjection.CountIndex;

            var slotAccess = rowSlots[slotIndex].Access(rowParameter);

            var predicate = BuildHavingPredicateBody(clause, slotAccess, inMemory);

            combined = combined is null ? predicate : Expression.AndAlso(combined, predicate);
        }

        if (combined is null)
        {
            return projected;
        }

        var lambda = Expression.Lambda(combined, rowParameter);
        return (IQueryable)_whereMethod
            .MakeGenericMethod(carrierType)
            .Invoke(null, [projected, lambda])!;
    }

    private static Expression BuildHavingPredicateBody(
        HavingPredicate clause,
        Expression slotAccess,
        bool inMemory)
    {
        // Walk the GraphQL having: { ... } AST through HotChocolate.Data's FilterVisitor. HavingFilterContext
        // overrides the operand parameter type — without it the visitor's lambda parameter would be
        // typed as the filter input class itself (HotChocolate.Data sets EntityType to the schema class for
        // *OperationFilterInputType subclasses) and operator handlers would emit ill-typed
        // comparisons. We push the carrier's slot CLR type so the lambda compiles cleanly.
        var operandRuntimeType = _typeInspector.GetType(slotAccess.Type);
        var ctx = new HavingFilterContext(clause.FilterType, operandRuntimeType, inMemory);
        _havingVisitor.Visit(clause.FilterValue, ctx);

        if (ctx.Errors.Count > 0)
        {
            throw new NotSupportedException(
                "Could not compile grouping HAVING predicate: "
                + string.Join("; ", ctx.Errors.Select(e => e.Message)));
        }

        if (!ctx.TryCreateLambda(out var lambda) || lambda is null)
        {
            throw new NotSupportedException("Could not compile grouping HAVING predicate.");
        }

        // Visitor builds a lambda over its own parameter (typed as the slot CLR). Replace that
        // parameter with the carrier's slot access so the predicate folds into the Where() over
        // the projected row.
        return new ParameterRebinder(lambda.Parameters[0], slotAccess).Visit(lambda.Body);
    }

    private sealed class ParameterRebinder(ParameterExpression source, Expression replacement)
        : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) =>
            node == source ? replacement : base.VisitParameter(node);
    }

    private static IEnumerable<GroupingResult<T>> Materialise<T>(IEnumerable rows, Type carrierType, SelectionPlan plan)
    {
        var rowSlots = RowProjection.Slots(carrierType, plan.Aggregates.Length);
        var keySlot = rowSlots[RowProjection.KeyIndex];
        var countSlot = rowSlots[RowProjection.CountIndex];

        var keySlots = KeyProjection.Slots(keySlot.Type, plan.KeyPaths.Length);

        foreach (var row in rows)
        {
            var builder = new GroupingResultBuilder<T>()
                .SetCount((int)countSlot.GetValue(row)!);

            var keyValue = keySlot.GetValue(row)!;
            for (var i = 0; i < plan.KeyPaths.Length; i++)
            {
                if (plan.KeyPaths[i].Length == 0)
                {
                    continue;
                }
                builder.SetKey(plan.KeyPaths[i], keySlots[i].GetValue(keyValue));
            }

            for (var i = 0; i < plan.Aggregates.Length; i++)
            {
                var slot = rowSlots[RowProjection.AggregateIndex(i)];
                builder.SetAggregate(plan.Aggregates[i].Path, plan.Aggregates[i].Kind, slot.GetValue(row));
            }

            yield return builder.Build();
        }
    }
}
