using HotChocolate.Data.Grouping.Aggregates;
using HotChocolate.Data.Grouping.Fields;
using HotChocolate.Types.Descriptors;

namespace HotChocolate.Data.Grouping.Convention;

/// <summary>
/// Default <see cref="IGroupingConventionDescriptor"/>.
/// </summary>
internal sealed class GroupingConventionDescriptor : IGroupingConventionDescriptor
{
    private GroupingConventionDescriptor(IDescriptorContext context, string? scope)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        Configuration.Scope = scope;
    }

    private IDescriptorContext Context { get; }

    private GroupingConventionConfiguration Configuration { get; } = new();

    public GroupingConventionConfiguration CreateConfiguration() => Configuration;

    /// <inheritdoc />
    public IGroupingConventionDescriptor BindScalarKind(Type runtimeType, GroupingScalarKind kind)
    {
        ArgumentNullException.ThrowIfNull(runtimeType);
        var key = Nullable.GetUnderlyingType(runtimeType) ?? runtimeType;
        Configuration.ScalarKinds[key] = kind;
        return this;
    }

    /// <inheritdoc />
    public IGroupingConventionDescriptor BindComparable<TRuntime>() =>
        BindScalarKind(typeof(TRuntime), GroupingScalarKind.Comparable);

    /// <inheritdoc />
    public IGroupingConventionDescriptor BindNumeric<TRuntime>() =>
        BindScalarKind(typeof(TRuntime), GroupingScalarKind.Numeric);

    /// <inheritdoc />
    public IGroupingConventionDescriptor BindRuntimeType<TRuntime, TResult>()
        where TResult : AggregateResultType =>
        BindRuntimeType(typeof(TRuntime), typeof(TResult));

    /// <inheritdoc />
    public IGroupingConventionDescriptor BindRuntimeType(Type runtimeType, Type resultType)
    {
        ArgumentNullException.ThrowIfNull(runtimeType);
        ArgumentNullException.ThrowIfNull(resultType);

        if (!typeof(AggregateResultType).IsAssignableFrom(resultType))
        {
            throw new ArgumentException(
                $"Type {resultType.FullName} does not derive from {nameof(AggregateResultType)}.",
                nameof(resultType));
        }

        var key = Nullable.GetUnderlyingType(runtimeType) ?? runtimeType;
        Configuration.AggregateBindings[key] = new AggregateBinding(resultType);

        if (!Configuration.ScalarKinds.ContainsKey(key))
        {
            Configuration.ScalarKinds[key] = GroupingScalarKind.Comparable;
        }
        return this;
    }

    /// <inheritdoc />
    public IGroupingConventionDescriptor DefaultFilterNullParent(bool value)
    {
        Configuration.DefaultFilterNullParent = value;
        return this;
    }

    /// <inheritdoc />
    public IGroupingConventionDescriptor AllowedAggregations(GroupingAggregations aggregations)
    {
        Configuration.AllowedAggregations = aggregations;
        return this;
    }

    /// <inheritdoc />
    public IGroupingConventionDescriptor GroupingProvider<TProvider>()
        where TProvider : class, IGroupingProvider
    {
        Configuration.GroupingProvider = typeof(TProvider);
        Configuration.GroupingProviderInstance = null;
        return this;
    }

    /// <inheritdoc />
    public IGroupingConventionDescriptor GroupingProvider<TProvider>(TProvider provider)
        where TProvider : class, IGroupingProvider
    {
        ArgumentNullException.ThrowIfNull(provider);
        Configuration.GroupingProviderInstance = provider;
        Configuration.GroupingProvider = typeof(TProvider);
        return this;
    }

    /// <inheritdoc />
    public IGroupingConventionDescriptor GroupingProvider(Type provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        if (!typeof(IGroupingProvider).IsAssignableFrom(provider))
        {
            throw new ArgumentException(
                $"Type {provider.FullName} does not implement {nameof(IGroupingProvider)}.",
                nameof(provider));
        }

        Configuration.GroupingProvider = provider;
        Configuration.GroupingProviderInstance = null;
        return this;
    }

    public static GroupingConventionDescriptor New(IDescriptorContext context, string? scope) =>
        new(context, scope);
}
