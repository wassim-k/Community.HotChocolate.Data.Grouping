using HotChocolate.Data.Grouping.Aggregates;
using HotChocolate.Data.Grouping.Fields;

namespace HotChocolate.Data.Grouping.Convention;

/// <summary>
/// Configures a <see cref="GroupingConvention"/>. Grouping-only surface — scalar leaf kinds,
/// aggregate-result bindings, the grouping provider, and the default <c>filterNullParent</c>.
/// All filter-side concerns (operation names, custom operators, expression handlers, per-type
/// overlays) are configured separately via HotChocolate.Data's <c>IFilterConventionDescriptor</c> exposed
/// through <c>services.AddFiltering(...)</c>.
/// </summary>
public interface IGroupingConventionDescriptor
{
    /// <summary>
    /// Registers <paramref name="runtimeType"/> as a scalar leaf of the supplied
    /// <paramref name="kind"/>. Most consumers use <see cref="BindComparable{TRuntime}"/> or
    /// <see cref="BindNumeric{TRuntime}"/>; this overload is for runtime-known types.
    /// </summary>
    IGroupingConventionDescriptor BindScalarKind(Type runtimeType, GroupingScalarKind kind);

    /// <summary>
    /// Registers <typeparamref name="TRuntime"/> as a <see cref="GroupingScalarKind.Comparable"/> leaf.
    /// </summary>
    IGroupingConventionDescriptor BindComparable<TRuntime>();

    /// <summary>
    /// Registers <typeparamref name="TRuntime"/> as a <see cref="GroupingScalarKind.Numeric"/> leaf.
    /// </summary>
    IGroupingConventionDescriptor BindNumeric<TRuntime>();

    /// <summary>
    /// Binds a runtime CLR scalar to a custom <c>*AggregateResultType</c>. The result type's
    /// own <c>Configure</c> declares each aggregation's HAVING filter input via
    /// <c>.Having&lt;TRuntime&gt;()</c>, so the binding is only concerned with the output shape.
    /// </summary>
    /// <remarks>
    /// MIN / MAX target <c>TRuntime?</c>. AVG and SUM are supported only when
    /// <typeparamref name="TRuntime"/> is a numeric scalar (see <c>AggregateWidening</c>);
    /// non-numeric custom scalars throw on AVG/SUM. Implicitly registers
    /// <typeparamref name="TRuntime"/> as <see cref="GroupingScalarKind.Comparable"/> if no prior
    /// <see cref="BindScalarKind(Type, GroupingScalarKind)"/> call set its kind. For HAVING to work on
    /// <typeparamref name="TRuntime"/>, the consumer must also register the matching filter input
    /// type via <c>AddFiltering(f =&gt; f.BindRuntimeType&lt;TRuntime, TFilter&gt;())</c>.
    /// </remarks>
    IGroupingConventionDescriptor BindRuntimeType<TRuntime, TResult>()
        where TResult : AggregateResultType;

    /// <summary>
    /// Untyped <see cref="BindRuntimeType{TRuntime, TResult}"/> overload.
    /// </summary>
    IGroupingConventionDescriptor BindRuntimeType(Type runtimeType, Type resultType);

    /// <summary>
    /// Selects the <see cref="IGroupingProvider"/> that runs the GROUP-BY pipeline.
    /// </summary>
    IGroupingConventionDescriptor GroupingProvider<TProvider>()
        where TProvider : class, IGroupingProvider;

    /// <summary>
    /// Selects a pre-constructed <see cref="IGroupingProvider"/> instance.
    /// </summary>
    IGroupingConventionDescriptor GroupingProvider<TProvider>(TProvider provider)
        where TProvider : class, IGroupingProvider;

    /// <summary>
    /// Untyped <see cref="GroupingProvider{TProvider}()"/> overload.
    /// </summary>
    IGroupingConventionDescriptor GroupingProvider(Type provider);

    /// <summary>
    /// Default for the auto-generated <c>filterNullParent: Boolean</c> argument on every <c>groupBy*</c> field.
    /// </summary>
    IGroupingConventionDescriptor DefaultFilterNullParent(bool value);

    /// <summary>
    /// Restricts which aggregate operations are exposed on every <c>*AggregateResult</c> type in
    /// the schema. Combine values with bitwise OR (e.g.
    /// <c>GroupingAggregations.Min | GroupingAggregations.Max</c>). Use <see cref="GroupingAggregations.None"/>
    /// to drop the <c>aggregate</c> field entirely. Defaults to <see cref="GroupingAggregations.All"/>.
    /// </summary>
    IGroupingConventionDescriptor AllowedAggregations(GroupingAggregations aggregations);
}
