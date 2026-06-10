using HotChocolate.Data.Grouping.Execution.QueryableGrouping;
using HotChocolate.Data.Grouping.Fields;
using HotChocolate.Types.Descriptors;
using static Microsoft.Extensions.DependencyInjection.ActivatorUtilities;

namespace HotChocolate.Data.Grouping.Convention;

/// <summary>
/// Default <see cref="IGroupingConvention"/>. Owns grouping-side state only — scalar kinds,
/// aggregate-result bindings, and the resolved <see cref="IGroupingProvider"/>. HAVING-predicate
/// compilation runs through HotChocolate.Data's filter convention which the consumer configures via
/// <c>AddFiltering(...)</c>.
/// </summary>
public class GroupingConvention : Convention<GroupingConventionConfiguration>, IGroupingConvention
{
    private Action<IGroupingConventionDescriptor>? _configure;

    private IReadOnlyDictionary<Type, GroupingScalarKind> _scalarKinds = null!;
    private IReadOnlyDictionary<Type, AggregateBinding> _aggregateBindings = null!;
    private bool _defaultFilterNullParent;
    private GroupingAggregations _allowedAggregations;
    private IGroupingProvider _groupingProvider = null!;

    protected GroupingConvention()
    {
        _configure = Configure;
    }

    public GroupingConvention(Action<IGroupingConventionDescriptor> configure)
    {
        _configure = configure ?? throw new ArgumentNullException(nameof(configure));
    }

    protected virtual void Configure(IGroupingConventionDescriptor descriptor) { }

    /// <inheritdoc />
    protected override GroupingConventionConfiguration CreateConfiguration(IConventionContext context)
    {
        if (_configure is null)
        {
            throw new InvalidOperationException(
                "No configuration was specified for the grouping convention.");
        }

        var descriptor = GroupingConventionDescriptor.New(context.DescriptorContext, context.Scope);
        _configure(descriptor);
        _configure = null;

        return descriptor.CreateConfiguration();
    }

    /// <inheritdoc />
    protected override void Complete(IConventionContext context)
    {
        if (Configuration is not { } groupingConfig)
        {
            throw new InvalidOperationException(
                "GroupingConvention completed without a configuration. "
                + "Ensure the convention is registered through AddGrouping().");
        }

        _scalarKinds = new Dictionary<Type, GroupingScalarKind>(groupingConfig.ScalarKinds);
        _aggregateBindings = new Dictionary<Type, AggregateBinding>(groupingConfig.AggregateBindings);
        _defaultFilterNullParent = groupingConfig.DefaultFilterNullParent;
        _allowedAggregations = groupingConfig.AllowedAggregations;
        _groupingProvider = ResolveGroupingProvider(context.Services, groupingConfig);

        base.Complete(context);
    }

    /// <inheritdoc />
    public bool DefaultFilterNullParent => _defaultFilterNullParent;

    /// <inheritdoc />
    public GroupingAggregations AllowedAggregations => _allowedAggregations;

    /// <inheritdoc />
    public IGroupingProvider Provider => _groupingProvider;

    /// <inheritdoc />
    public bool IsScalar(Type runtimeType)
    {
        ArgumentNullException.ThrowIfNull(runtimeType);
        var key = Nullable.GetUnderlyingType(runtimeType) ?? runtimeType;
        return key.IsEnum || _scalarKinds.ContainsKey(key);
    }

    /// <inheritdoc />
    public Type? ResolveAggregateResultType(Type sourceClr)
    {
        ArgumentNullException.ThrowIfNull(sourceClr);
        var key = Nullable.GetUnderlyingType(sourceClr) ?? sourceClr;
        if (!_aggregateBindings.TryGetValue(key, out var binding))
        {
            return null;
        }

        // Comparable scalars expose only Min/Max — skip them when the user's flags exclude both,
        // otherwise the *AggregateResult ends up with no fields and HC fails schema build.
        if (_scalarKinds.TryGetValue(key, out var scalarKind) &&
            scalarKind == GroupingScalarKind.Comparable &&
            (_allowedAggregations & (GroupingAggregations.Min | GroupingAggregations.Max)) == 0)
        {
            return null;
        }

        return binding.ResultType;
    }

    private static IGroupingProvider ResolveGroupingProvider(
        IServiceProvider services,
        GroupingConventionConfiguration configuration)
    {
        if (configuration.GroupingProviderInstance is { } instance)
        {
            return instance;
        }

        var providerType = configuration.GroupingProvider ?? typeof(QueryableGroupingProvider);
        return (IGroupingProvider)GetServiceOrCreateInstance(services, providerType);
    }
}
