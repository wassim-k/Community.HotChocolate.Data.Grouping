using HotChocolate.Configuration;
using HotChocolate.Data.Grouping.Convention;
using HotChocolate.Types.Descriptors;

namespace HotChocolate.Data.Grouping;

/// <summary>
/// Helpers to resolve an <see cref="IGroupingConvention"/> from a HotChocolate descriptor context.
/// </summary>
public static class GroupingDescriptorContextExtensions
{
    /// <summary>
    /// Resolves the <see cref="IGroupingConvention"/> registered on <paramref name="context"/>.
    /// </summary>
    public static IGroupingConvention GetGroupingConvention(
        this ITypeSystemObjectContext context,
        string? scope = null) =>
        context.DescriptorContext.GetGroupingConvention(scope);

    /// <summary>
    /// Resolves the <see cref="IGroupingConvention"/> registered on <paramref name="context"/>.
    /// </summary>
    public static IGroupingConvention GetGroupingConvention(
        this IDescriptorContext context,
        string? scope = null) =>
        context.GetConventionOrDefault<IGroupingConvention>(
            defaultConvention: () => throw ThrowHelper.GroupingDescriptorContextExtensions_NoConvention(scope),
            scope);
}
