#pragma warning disable IDE0130 // Namespace does not match folder structure

using HotChocolate.Data.Grouping.Config;
using HotChocolate.Data.Grouping.Convention;
using HotChocolate.Data.Grouping.Types;
using HotChocolate.Execution.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HotChocolate.Data.Grouping;

/// <summary>
/// Provides grouping-related extensions for the <see cref="IRequestExecutorBuilder"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Optional HAVING.</b> Grouping does NOT force HotChocolate.Data filtering on consumers. If
/// <c>AddFiltering(...)</c> is also registered, the <c>having:</c> argument surfaces on
/// <c>count</c> and every aggregate operation; if not, those arguments are silently omitted from
/// the schema and grouping still works for key/aggregate selection. To turn HAVING on, call
/// <c>AddFiltering()</c> alongside <c>AddGrouping()</c>.
/// </para>
/// <para>
/// <b>HAVING composition semantics</b> (when filtering is available). HAVING predicates compose
/// via implicit AND only:
/// </para>
/// <list type="bullet">
///   <item>Multiple operators inside one <c>having:</c> clause are AND-combined —
///   <c>count(having: { gt: 2, lt: 10 })</c> means <c>count &gt; 2 AND count &lt; 10</c>.</item>
///   <item>HAVING clauses on different aggregate fields are AND-combined at the bucket level —
///   <c>count(having: {...}) avg(having: {...})</c> keeps only buckets passing both.</item>
/// </list>
/// <para>
/// Explicit <c>or: [...]</c> / <c>and: [...]</c> composition fields are deliberately not exposed
/// on the comparable scalar family (Int/Long/Decimal/Float/Boolean/etc.): HotChocolate.Data ships those
/// types with and/or disabled, and re-enabling globally would mutate types shared with regular
/// <c>[UseFiltering]</c> consumers. To express OR semantics, split into multiple aggregate
/// operations or restructure the query.
/// </para>
/// </remarks>
public static class GroupingSchemaBuilderExtensions
{
    /// <summary>
    /// Adds grouping support to the executor using the default <see cref="GroupingConvention"/>
    /// with the built-in scalar bindings. Mirrors <c>AddFiltering()</c>.
    /// </summary>
    public static IRequestExecutorBuilder AddGrouping(
        this IRequestExecutorBuilder builder,
        string? scope = null) =>
        AddGrouping(builder, d => d.AddDefaults(), scope);

    /// <summary>
    /// Adds grouping support using a custom convention type. The custom convention's
    /// <c>Configure(IGroupingConventionDescriptor)</c> override is responsible for calling
    /// <c>descriptor.AddDefaults()</c> if it wants the built-in bindings.
    /// </summary>
    public static IRequestExecutorBuilder AddGrouping<TConvention>(
        this IRequestExecutorBuilder builder,
        string? scope = null)
        where TConvention : class, IGroupingConvention
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.TryAddConvention<IGroupingConvention, TConvention>(scope);
        builder.RegisterGroupingConfigStore();
        return builder;
    }

    /// <summary>
    /// Adds grouping support and configures the default convention inline. The supplied
    /// <paramref name="configure"/> delegate is called as-is — call <c>d.AddDefaults()</c>
    /// inside it if you want the built-in bindings.
    /// </summary>
    public static IRequestExecutorBuilder AddGrouping(
        this IRequestExecutorBuilder builder,
        Action<IGroupingConventionDescriptor> configure,
        string? scope = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        builder.TryAddConvention<IGroupingConvention>(_ => new GroupingConvention(configure), scope);
        builder.RegisterGroupingConfigStore();
        return builder;
    }

    /// <summary>
    /// Registers an explicit <see cref="GroupingConfig{T}"/> for an entity. Call after <c>.AddGrouping()</c>.
    /// </summary>
    public static IRequestExecutorBuilder AddGroupingConfig<TConfig>(
        this IRequestExecutorBuilder builder)
        where TConfig : GroupingConfig
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ConfigureSchemaServices(
            s => s.TryAddEnumerable(ServiceDescriptor.Singleton<GroupingConfig, TConfig>()));
        return builder;
    }

    // Idempotent: store + interceptor are global per schema regardless of convention count.
    private static IRequestExecutorBuilder RegisterGroupingConfigStore(this IRequestExecutorBuilder builder)
    {
        builder.ConfigureSchemaServices(s => s.TryAddSingleton<GroupingConfigStore>());
        builder.TryAddTypeInterceptor<GroupingConfigInterceptor>();
        return builder;
    }

}

