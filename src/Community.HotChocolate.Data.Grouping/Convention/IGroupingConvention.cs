using HotChocolate.Types.Descriptors;

namespace HotChocolate.Data.Grouping.Convention;

/// <summary>
/// Owns the schema-build-time registry of scalar leaves, aggregate-result bindings, the resolved
/// <see cref="IGroupingProvider"/>, and default argument behaviour. The HAVING runtime delegates
/// filter-side concerns (operations, runtime→filter input bindings, configure-overlays, expression
/// handlers) to HotChocolate.Data's <see cref="HotChocolate.Data.Filters.IFilterConvention"/> — register
/// those via <c>services.AddFiltering(...)</c>.
/// </summary>
public interface IGroupingConvention : IConvention
{
    /// <summary>Default for the <c>filterNullParent</c> argument on every <c>groupBy*</c> field.</summary>
    bool DefaultFilterNullParent { get; }

    /// <summary>The grouping provider the middleware delegates to.</summary>
    IGroupingProvider Provider { get; }

    /// <summary>
    /// True when <paramref name="runtimeType"/> is a scalar leaf (grouped on directly).
    /// </summary>
    /// <remarks>Enums are always leaves; <see cref="Nullable{T}"/> is unwrapped before lookup.</remarks>
    bool IsScalar(Type runtimeType);

    /// <summary>
    /// Resolves the <c>*AggregateResult</c> schema type for a leaf, or <see langword="null"/> when unsupported.
    /// </summary>
    Type? ResolveAggregateResultType(Type sourceClr);
}
