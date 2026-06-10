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

        var (plan, having) = new SelectionParser(context, convention).Parse();
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
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (AggregateCollectionMissingFromKeyException ex)
        {
            throw Grouping_AggregateCollectionMissingFromKey(
                context,
                ex.Kind.ToString().ToLowerInvariant(),
                FormatPath(ex.AggregatePath));
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

    private static string FormatPath(PathSegment[] path) =>
        string.Join('.', path.Select(s => char.ToLowerInvariant(s.Property.Name[0]) + s.Property.Name[1..]));

    private sealed class SelectionParser(IMiddlewareContext context, IGroupingConvention convention)
    {
        private readonly List<PathSegment[]> _keyPaths = [];
        private readonly HashSet<string> _seenKeyPaths = new(StringComparer.Ordinal);
        private readonly List<AggregateRequest> _aggregates = [];
        private readonly Dictionary<string, int> _aggregateIndices = new(StringComparer.Ordinal);
        private readonly List<HavingPredicate> _havings = [];

        public (SelectionPlan Plan, IReadOnlyList<HavingPredicate> Having) Parse()
        {
            foreach (var selection in ChildSelections(context.Selection))
            {
                if (!IsIncluded(selection))
                {
                    continue;
                }

                if (selection.Field.Name == GroupingFieldNames.Key && selection.HasSelections)
                {
                    CollectKeyPaths(selection, []);
                }
                else if (selection.Field.Name == GroupingFieldNames.Count)
                {
                    // having on count uses AggregateIndex = null to gate the bucket by row count.
                    AddHaving(selection, aggregateIndex: null);
                }
                else if (selection.Field.Name == GroupingFieldNames.Aggregate && selection.HasSelections)
                {
                    WalkAggregate(selection, []);
                }
            }

            // No key selected: a single empty path groups the entire source into one bucket.
            PathSegment[][] keyPaths = _keyPaths.Count == 0 ? [[]] : [.. _keyPaths];
            return (new SelectionPlan(keyPaths, [.. _aggregates]), _havings);
        }

        private void CollectKeyPaths(Selection parent, PathSegment[] ancestors)
        {
            foreach (var selection in ChildSelections(parent))
            {
                if (!IsIncluded(selection) || selection.Field.Member is not PropertyInfo property)
                {
                    continue;
                }

                var path = (PathSegment[])[.. ancestors, new PathSegment(property, Classify(property))];

                if (selection.HasSelections)
                {
                    CollectKeyPaths(selection, path);
                }
                else if (_seenKeyPaths.Add(GroupingHelpers.StringifyPrefix(path)))
                {
                    _keyPaths.Add(path);
                }
            }
        }

        private void WalkAggregate(Selection parent, PathSegment[] ancestors)
        {
            foreach (var selection in ChildSelections(parent))
            {
                if (!IsIncluded(selection)
                    || selection.Field.Member is not PropertyInfo property
                    || !selection.HasSelections)
                {
                    continue;
                }

                var segment = new PathSegment(property, Classify(property));
                var path = (PathSegment[])[.. ancestors, segment];

                if (segment.Kind is PathSegmentKind.Scalar or PathSegmentKind.PrimitiveCollection)
                {
                    CollectLeafOperations(selection, path);
                }
                else
                {
                    WalkAggregate(selection, path);
                }
            }
        }

        private void CollectLeafOperations(Selection leaf, PathSegment[] path)
        {
            foreach (var opSelection in ChildSelections(leaf))
            {
                if (!IsIncluded(opSelection)
                    || !GroupingFieldNames.TryParse(opSelection.Field.Name, out var kind))
                {
                    continue;
                }

                // Aliased duplicates of the same operation share one carrier slot; their having
                // clauses each reference that slot and AND-compose at the bucket level.
                var requestKey = $"{GroupingHelpers.StringifyPrefix(path)}:{kind}";
                if (!_aggregateIndices.TryGetValue(requestKey, out var index))
                {
                    index = _aggregates.Count;
                    _aggregates.Add(new AggregateRequest(path, kind));
                    _aggregateIndices[requestKey] = index;
                }

                AddHaving(opSelection, index);
            }
        }

        private void AddHaving(Selection selection, int? aggregateIndex)
        {
            if (selection.Field.Arguments.FirstOrDefault(a => a.Name == GroupingArgumentNames.Having)
                ?.Type.NamedType() is not IFilterInputType filterType)
            {
                return;
            }

            foreach (var syntaxNode in selection.SyntaxNodes)
            {
                foreach (var arg in syntaxNode.Node.Arguments)
                {
                    if (arg.Name.Value != GroupingArgumentNames.Having || arg.Value is NullValueNode)
                    {
                        continue;
                    }

                    var resolved = VariableRewriter.Rewrite(arg.Value, filterType, NullValueNode.Default, context.Variables);
                    if (resolved is not NullValueNode)
                    {
                        _havings.Add(new HavingPredicate(aggregateIndex, resolved, filterType));
                    }

                    return;
                }
            }
        }

        private bool IsIncluded(Selection selection) => selection.IsIncluded(context.IncludeFlags);

        private PathSegmentKind Classify(PropertyInfo property) => MemberClassifier.Classify(property, convention).Kind;

        private static ReadOnlySpan<Selection> ChildSelections(Selection parent) =>
            parent.GetSelectionSet((ObjectType)parent.Field.Type.NamedType()).Selections;
    }
}
