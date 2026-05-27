using HotChocolate.Resolvers;

namespace HotChocolate.Data.Grouping;

internal static class ThrowHelper
{
    public static SchemaException GroupObjectFieldDescriptorExtensions_CannotInfer(Type resultType)
        => new SchemaException(
            SchemaErrorBuilder.New()
                .SetMessage(
                    "[UseGrouping] could not infer an entity type from the field's runtime type `{0}`. "
                    + "Expose the field with a concrete element type `T` recognised by the configured "
                    + "grouping provider (not `object`, an anonymous type, or a bare collection).",
                    resultType.FullName ?? resultType.Name)
                .SetCode(GroupingErrorCodes.NotSupported)
                .Build());

    public static SchemaException Grouping_ReservedEntityName(string entityName)
        => new SchemaException(
            SchemaErrorBuilder.New()
                .SetMessage(
                    "An entity exposed to grouping cannot be named `{0}` — this name is reserved "
                    + "by the generated grouping schema types. Rename the entity (e.g. via "
                    + "`ObjectType<T>.Name(...)`) or pick a different CLR type name.",
                    entityName)
                .SetCode(GroupingErrorCodes.NotSupported)
                .Build());

    public static GraphQLException Grouping_NotSupported(
        IMiddlewareContext context,
        NotSupportedException exception)
        => new GraphQLException(
            ErrorBuilder.New()
                .SetMessage(exception.Message)
                .SetCode(GroupingErrorCodes.NotSupported)
                .SetPath(context.Path)
                .AddLocation(context.Selection.SyntaxNodes[0].Node)
                .Build());

    public static GraphQLException Grouping_ResolverFailed(
        IMiddlewareContext context,
        Exception exception)
        => new GraphQLException(
            ErrorBuilder.New()
                .SetMessage(
                    "Grouping resolver failed: {0}",
                    exception.Message)
                .SetCode(GroupingErrorCodes.ResolverFailed)
                .SetPath(context.Path)
                .AddLocation(context.Selection.SyntaxNodes[0].Node)
                .SetException(exception)
                .Build());

    public static InvalidOperationException Grouping_UnknownAggregationKind()
        => new InvalidOperationException("Unknown aggregation kind.");

    public static SchemaException GroupingDescriptorContextExtensions_NoConvention(string? scope)
        => new SchemaException(
            SchemaErrorBuilder.New()
                .SetMessage(
                    scope is null
                        ? "No grouping convention has been registered. Call "
                          + "`AddGrouping()` on your `IRequestExecutorBuilder` before using "
                          + "`[UseGrouping]`."
                        : "No grouping convention has been registered for scope `{0}`. "
                          + "Call `AddGrouping(scope: \"{0}\")` on your "
                          + "`IRequestExecutorBuilder` before using `[UseGrouping(Scope = \"{0}\")]`.",
                    scope ?? string.Empty)
                .SetCode(GroupingErrorCodes.NotSupported)
                .Build());

    public static GraphQLException Grouping_AggregateCollectionMissingFromKey(
        IMiddlewareContext context,
        string aggregateOperation,
        string aggregatePath,
        string collectionPath)
        => new GraphQLException(
            ErrorBuilder.New()
                .SetMessage(
                    "Cannot use `{0}.{1}` in `aggregate` when `key` doesn't include a path through `{2}`. "
                    + "Selecting a collection navigation inside `aggregate` expands the source into one "
                    + "row per element, which changes which buckets are returned and what `count` means. "
                    + "Include the same collection in `key` so the expansion is visible at the call site, "
                    + "or remove `{1}` from `aggregate`.",
                    aggregateOperation,
                    aggregatePath,
                    collectionPath)
                .SetCode(GroupingErrorCodes.AggregateCollectionMissingFromKey)
                .SetPath(context.Path)
                .AddLocation(context.Selection.SyntaxNodes[0].Node)
                .Build());
}
