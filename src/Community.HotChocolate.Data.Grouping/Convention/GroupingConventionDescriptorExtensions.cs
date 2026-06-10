using HotChocolate.Data.Grouping.Aggregates;
using HotChocolate.Data.Grouping.Execution.QueryableGrouping;

namespace HotChocolate.Data.Grouping.Convention;

/// <summary>
/// Fluent helpers consumers apply on <see cref="IGroupingConventionDescriptor"/> to seed
/// built-in scalar bindings + the grouping provider. Filter-side concerns (operations, handlers,
/// custom operators, per-type overlays) are configured separately via HotChocolate.Data's <c>AddFiltering()</c>.
/// </summary>
public static class GroupingConventionDescriptorExtensions
{
    /// <summary>
    /// Binds every built-in CLR scalar to its <c>*AggregateResultType</c> and a scalar-kind
    /// classification (<see cref="GroupingScalarKind.Numeric"/> vs.
    /// <see cref="GroupingScalarKind.Comparable"/>). The HAVING filter input type per aggregation
    /// is declared on each result type's <c>Configure</c> via <c>.Having&lt;TRuntime&gt;()</c>.
    /// </summary>
    public static IGroupingConventionDescriptor BindDefaultTypes(
        this IGroupingConventionDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        foreach (var binding in DefaultGroupingBindings.All)
        {
            descriptor.BindScalarKind(binding.Runtime, binding.Kind);
            descriptor.BindRuntimeType(binding.Runtime, binding.Result);
        }
        return descriptor;
    }

    /// <summary>
    /// Binds the default scalar set and registers the queryable grouping provider. The zero-arg
    /// <c>AddGrouping()</c> extension calls this automatically; consumers with an inline configure
    /// delegate must call it themselves (or any subset they want).
    /// </summary>
    public static IGroupingConventionDescriptor AddDefaults(
        this IGroupingConventionDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        descriptor.BindDefaultTypes();
        descriptor.GroupingProvider<QueryableGroupingProvider>();
        return descriptor;
    }
}
