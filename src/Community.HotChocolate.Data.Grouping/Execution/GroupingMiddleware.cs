using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using HotChocolate.Data.Filters;
using HotChocolate.Data.Grouping.Convention;
using HotChocolate.Data.Grouping.Execution.QueryableGrouping;
using HotChocolate.Data.Grouping.Fields;
using HotChocolate.Data.Grouping.Naming;
using HotChocolate.Execution.Processing;
using HotChocolate.Language;
using HotChocolate.Resolvers;
using HotChocolate.Types;
using static HotChocolate.Data.Grouping.Expressions.ExpressionUtilities;
using static HotChocolate.Data.Grouping.ThrowHelper;

namespace HotChocolate.Data.Grouping.Execution;

internal sealed class GroupingMiddleware<T>(FieldDelegate next, IGroupingConvention convention)
{
    public async Task InvokeAsync(IMiddlewareContext context)
    {
        await next(context).ConfigureAwait(false);

        if (context.Result is null)
        {
            return;
        }

        var (plan, having) = ParseSelection(context, convention);

        ValidateAggregateCollectionsCoveredByKey(context, plan);

        if (plan.KeyPaths.Length == 0)
        {
            plan = plan with { KeyPaths = [[]] };
        }

        var filterNullParent = context.ArgumentValue<bool>(GroupingArgumentNames.FilterNullParent);

        try
        {
            var groups = await convention.Provider
                .ApplyAsync<T>(context.Result, plan, filterNullParent, having, context, context.RequestAborted)
                .ConfigureAwait(false);

            if (groups is not null)
            {
                context.Result = groups;
            }
        }
        catch (NotSupportedException ex)
        {
            throw Grouping_NotSupported(context, ex);
        }
        catch (Exception ex) when (ex is not GraphQLException)
        {
            throw Grouping_ResolverFailed(context, ex);
        }
    }

    private static (SelectionPlan, IReadOnlyList<HavingPredicate>) ParseSelection(
        IMiddlewareContext context,
        IGroupingConvention convention)
    {
        var fieldSelections = context.Selection.SyntaxNodes[0].Node.SelectionSet?.Selections
                .OfType<FieldNode>()
            ?? [];

        var groupNamedType = (ObjectType)context.Selection.Field.Type.NamedType();

        PathSegment[][] keyPaths = [];
        var aggregateRequests = new List<AggregateRequest>();
        var havingClauses = new List<HavingPredicate>();

        foreach (var top in fieldSelections)
        {
            if (top.Name.Value == GroupingFieldNames.Key && top.SelectionSet is { } keySet)
            {
                var keyField = groupNamedType.Fields[GroupingFieldNames.Key];
                keyPaths = GetSelectionPropertyPaths(keySet, keyField.Type, convention);
            }
            else if (top.Name.Value == GroupingFieldNames.Count)
            {
                // having on count uses AggregateIndex = null to gate the bucket by row count.
                if (TryReadHaving(top, out var countHaving))
                {
                    var countField = groupNamedType.Fields[GroupingFieldNames.Count];
                    var filterType = ResolveHavingType(countField);
                    if (filterType is not null && TryResolveHaving(context, countHaving, filterType, out var resolved))
                    {
                        havingClauses.Add(new HavingPredicate(null, resolved, filterType));
                    }
                }
            }
            else if (top.Name.Value == GroupingFieldNames.Aggregate && top.SelectionSet is { } aggregateSet)
            {
                var aggregateType = (ObjectType)groupNamedType.Fields[GroupingFieldNames.Aggregate].Type.NamedType();
                WalkAggregate(context, aggregateSet, aggregateType, convention, [], aggregateRequests, havingClauses);
            }
        }

        var plan = new SelectionPlan(keyPaths, [.. aggregateRequests]);
        return (plan, havingClauses);
    }

    private static void WalkAggregate(
        IMiddlewareContext context,
        SelectionSetNode selectionSet,
        ObjectType parentType,
        IGroupingConvention convention,
        PathSegment[] ancestors,
        List<AggregateRequest> requests,
        List<HavingPredicate> havings)
    {
        foreach (var sel in selectionSet.Selections.OfType<FieldNode>())
        {
            if (!parentType.Fields.TryGetField(sel.Name.Value, out var field)
                || field.Member is not PropertyInfo property
                || sel.SelectionSet is not { } childSet)
            {
                continue;
            }

            var segment = new PathSegment(property, ClassifyKind(property, convention));
            var path = (PathSegment[])[.. ancestors, segment];

            var isScalar = segment.Kind is PathSegmentKind.Scalar or PathSegmentKind.PrimitiveCollection;

            if (isScalar)
            {
                foreach (var opSel in childSet.Selections.OfType<FieldNode>())
                {
                    if (!GroupingFieldNames.TryParse(opSel.Name.Value, out var kind))
                    {
                        continue;
                    }

                    var index = requests.Count;
                    requests.Add(new AggregateRequest(path, kind));

                    if (TryReadHaving(opSel, out var leafHaving))
                    {
                        var filterType = ResolveHavingType(field, opSel.Name.Value);
                        if (filterType is not null && TryResolveHaving(context, leafHaving, filterType, out var resolved))
                        {
                            havings.Add(new HavingPredicate(index, resolved, filterType));
                        }
                    }
                }
            }
            else
            {
                var navType = (ObjectType)field.Type.NamedType();
                WalkAggregate(context, childSet, navType, convention, path, requests, havings);
            }
        }
    }

    private static IFilterInputType? ResolveHavingType(ObjectField field, string? subFieldName = null)
    {
        // For aggregate leaf operations (avg/sum/min/max) we must dive into the leaf-level
        // object type and grab the named sub-field; the `having:` argument hangs off that sub-field.
        if (subFieldName is not null)
        {
            return field.Type.NamedType() is ObjectType nested
                && nested.Fields.TryGetField(subFieldName, out var subField)
                ? ResolveHavingType(subField)
                : null;
        }

        return field.Arguments.FirstOrDefault(a => a.Name == GroupingArgumentNames.Having)
            ?.Type.NamedType() as IFilterInputType;
    }

    private static bool TryResolveHaving(
        IMiddlewareContext context,
        IValueNode rawHaving,
        IFilterInputType filterType,
        [NotNullWhen(true)] out IValueNode? resolved)
    {
        resolved = VariableRewriter.Rewrite(rawHaving, filterType, NullValueNode.Default, context.Variables);
        return resolved is not NullValueNode;
    }

    private static bool TryReadHaving(FieldNode field, out IValueNode having)
    {
        foreach (var arg in field.Arguments)
        {
            if (arg.Name.Value == GroupingArgumentNames.Having && arg.Value is not NullValueNode)
            {
                having = arg.Value;
                return true;
            }
        }

        having = NullValueNode.Default;
        return false;
    }

    private static PathSegment[][] GetSelectionPropertyPaths(
        SelectionSetNode selectionSetNode,
        IOutputType outputType,
        IGroupingConvention convention,
        PathSegment[]? ancestors = null)
    {
        ancestors ??= [];
        var objectType = (ObjectType)outputType.NamedType();

        return [.. selectionSetNode.Selections
            .OfType<FieldNode>()
            .Where(field => field.Name.Value != "__typename")
            .SelectMany(selectionField =>
            {
                if (!objectType.Fields.TryGetField(selectionField.Name.Value, out var field) ||
                    field.Member is not PropertyInfo property)
                {
                    return [];
                }

                var path = (PathSegment[])[..ancestors, new PathSegment(property, ClassifyKind(property, convention))];

                return selectionField.SelectionSet is null
                    ? [path]
                    : GetSelectionPropertyPaths(selectionField.SelectionSet, field.Type, convention, path);
            })];
    }

    // Reject aggregate leaf paths whose collection prefix isn't covered by some key path —
    // otherwise SelectMany would silently drop entities without that collection and skew count.
    private static void ValidateAggregateCollectionsCoveredByKey(IMiddlewareContext context, SelectionPlan plan)
    {
        var keyPrefixes = new HashSet<string>(
            GroupingHelpers.ResolveCollectionPrefixes(plan.KeyPaths)
                .Select(p => GroupingHelpers.StringifyPrefix(p.Prefix)),
            StringComparer.Ordinal);

        foreach (var request in plan.Aggregates)
        {
            var prefixes = GroupingHelpers.ResolveCollectionPrefixes([request.Path]);
            foreach (var prefix in prefixes)
            {
                if (keyPrefixes.Contains(GroupingHelpers.StringifyPrefix(prefix.Prefix)))
                {
                    continue;
                }

                throw Grouping_AggregateCollectionMissingFromKey(
                    context,
                    request.Kind.ToString().ToLowerInvariant(),
                    FormatPath(request.Path),
                    FormatPath(prefix.Prefix));
            }
        }
    }

    private static string FormatPath(PathSegment[] path) =>
        string.Join('.', path.Select(s => char.ToLowerInvariant(s.Property.Name[0]) + s.Property.Name[1..]));

    private static PathSegmentKind ClassifyKind(PropertyInfo property, IGroupingConvention convention)
        => MemberClassifier.Classify(property, convention).Kind;
}
