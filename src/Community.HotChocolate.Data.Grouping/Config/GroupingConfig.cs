#pragma warning disable SA1402 // File may only contain a single type

namespace HotChocolate.Data.Grouping.Config;

/// <summary>
/// Non-generic base — use <see cref="GroupingConfig{T}"/> directly.
/// </summary>
public abstract class GroupingConfig
{
    internal abstract Type EntityType { get; }

    internal abstract GroupingConfigDefinition CreateDefinition();
}

/// <summary>
/// Provides explicit field configuration for all grouping schema types generated for entity <typeparamref name="T"/>.
/// </summary>
/// <remarks>
/// Takes priority over renames and ignores inherited from the consumer's <c>ObjectType&lt;T&gt;</c>.
/// </remarks>
public abstract class GroupingConfig<T> : GroupingConfig
{
    internal override Type EntityType => typeof(T);

    internal override GroupingConfigDefinition CreateDefinition()
    {
        var descriptor = new GroupingConfigDescriptor<T>();
        Configure(descriptor);
        return descriptor.CreateDefinition();
    }

    protected abstract void Configure(IGroupingConfigDescriptor<T> descriptor);
}
