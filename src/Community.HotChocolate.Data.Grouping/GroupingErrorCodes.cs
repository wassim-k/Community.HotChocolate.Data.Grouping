namespace HotChocolate.Data.Grouping;

/// <summary>Stable error codes surfaced on <c>GraphQLException.Code</c>.</summary>
public static class GroupingErrorCodes
{
    /// <summary>
    /// The provider couldn't translate the selection into a single <c>GROUP BY</c>.
    /// Original <see cref="NotSupportedException"/> message is preserved on the error.
    /// </summary>
    public const string NotSupported = "GROUPING_NOT_SUPPORTED";

    /// <summary>
    /// An exception escaped the provider during enumeration — typically a user property
    /// getter that threw. Cause is preserved on <c>error.Exception</c>.
    /// </summary>
    public const string ResolverFailed = "GROUPING_RESOLVER_FAILED";

    /// <summary>
    /// An <c>aggregate</c> leaf path traverses a collection that the <c>key</c> doesn't
    /// also traverse. Selecting a collection inside <c>aggregate</c> alone would silently
    /// change which buckets are returned (entities without that collection would vanish)
    /// and what <c>count</c> means. The collection must be included in <c>key</c> so the
    /// expansion is visible at the call site.
    /// </summary>
    public const string AggregateCollectionMissingFromKey = "GROUPING_AGGREGATE_COLLECTION_MISSING_FROM_KEY";
}
