using HotChocolate.Configuration;
using HotChocolate.Data.Grouping.Fields;
using HotChocolate.Types;
using HotChocolate.Types.Descriptors.Configurations;

namespace HotChocolate.Data.Grouping.Aggregates;

/// <summary>
/// Per-scalar output type exposing the operations applicable to a single leaf's <see cref="AggregateValues"/>.
/// </summary>
/// <remarks>
/// HAVING is applied earlier in the pipeline as a Where on the projected row; the schema-level
/// <c>having:</c> argument here is consumed by <see cref="Execution.GroupingMiddleware{T}"/> rather
/// than by a resolver.
/// </remarks>
public abstract class AggregateResultType : ObjectType<AggregateValues>
{
    private Action<IAggregateResultTypeDescriptor>? _configure;

    protected AggregateResultType()
    {
        _configure = Configure;
    }

    protected AggregateResultType(Action<IAggregateResultTypeDescriptor> configure)
    {
        _configure = configure ?? throw new ArgumentNullException(nameof(configure));
    }

    /// <inheritdoc />
    protected sealed override ObjectTypeConfiguration CreateConfiguration(ITypeDiscoveryContext context)
    {
        try
        {
            if (Configuration is null)
            {
                var descriptor = AggregateResultTypeDescriptor.New(context.DescriptorContext, context.Scope);
                _configure!(descriptor);
                Configuration = descriptor.CreateConfiguration();
            }

            return Configuration;
        }
        finally
        {
            _configure = null;
        }
    }

    /// <summary>
    /// Subclass extension point. Each per-scalar <c>*AggregateResultType</c> declares its full
    /// operation set explicitly here — schema name, supported aggregations, and the matching
    /// HotChocolate.Data <c>*OperationFilterInputType</c> used for the <c>having:</c> argument.
    /// Widened backing CLR types come from <see cref="AggregateWidening.Resolve"/> so the schema
    /// and the queryable materialisation layer agree by construction.
    /// </summary>
    protected virtual void Configure(IAggregateResultTypeDescriptor descriptor) { }

    protected sealed override void Configure(IObjectTypeDescriptor<AggregateValues> descriptor) =>
        throw new NotSupportedException();
}

/// <summary>
/// Aggregate result for numeric runtime types — exposes <c>avg</c>, <c>sum</c>, <c>min</c>, <c>max</c>.
/// Each operation's schema type and HAVING input come from <see cref="AggregateWidening.Resolve"/>,
/// so the projected row's CLR slot and the schema agree by construction (e.g. <c>avg(int)</c>
/// widens to <c>double</c>).
/// </summary>
public abstract class NumericAggregateResultType<T> : AggregateResultType
{
    protected override void Configure(IAggregateResultTypeDescriptor descriptor)
    {
        foreach (var kind in new[] { GroupingAggregations.Avg, GroupingAggregations.Sum, GroupingAggregations.Min, GroupingAggregations.Max })
        {
            var slot = AggregateWidening.Resolve(typeof(T), kind);
            descriptor.Operation(kind).Type(slot).Having(slot);
        }
    }
}

/// <summary>
/// Aggregate result for comparable (non-numeric) runtime types — exposes <c>min</c> and <c>max</c>
/// only. <c>avg</c> and <c>sum</c> have no meaning for these categories.
/// </summary>
public abstract class ComparableAggregateResultType<T> : AggregateResultType
{
    protected override void Configure(IAggregateResultTypeDescriptor descriptor)
    {
        foreach (var kind in new[] { GroupingAggregations.Min, GroupingAggregations.Max })
        {
            var slot = AggregateWidening.Resolve(typeof(T), kind);
            descriptor.Operation(kind).Type(slot).Having(slot);
        }
    }
}

internal sealed class IntAggregateResultType : NumericAggregateResultType<int>
{
    protected override void Configure(IAggregateResultTypeDescriptor descriptor)
    {
        descriptor.Name("IntAggregateResult");
        base.Configure(descriptor);
    }
}

internal sealed class UIntAggregateResultType : NumericAggregateResultType<uint>
{
    protected override void Configure(IAggregateResultTypeDescriptor descriptor)
    {
        descriptor.Name("UnsignedIntAggregateResult");
        base.Configure(descriptor);
    }
}

internal sealed class LongAggregateResultType : NumericAggregateResultType<long>
{
    protected override void Configure(IAggregateResultTypeDescriptor descriptor)
    {
        descriptor.Name("LongAggregateResult");
        base.Configure(descriptor);
    }
}

internal sealed class DecimalAggregateResultType : NumericAggregateResultType<decimal>
{
    protected override void Configure(IAggregateResultTypeDescriptor descriptor)
    {
        descriptor.Name("DecimalAggregateResult");
        base.Configure(descriptor);
    }
}

internal sealed class FloatAggregateResultType : NumericAggregateResultType<double>
{
    protected override void Configure(IAggregateResultTypeDescriptor descriptor)
    {
        descriptor.Name("FloatAggregateResult");
        base.Configure(descriptor);
    }
}

internal sealed class StringAggregateResultType : ComparableAggregateResultType<string>
{
    protected override void Configure(IAggregateResultTypeDescriptor descriptor)
    {
        descriptor.Name("StringAggregateResult");
        base.Configure(descriptor);
    }
}

internal sealed class BooleanAggregateResultType : ComparableAggregateResultType<bool>
{
    protected override void Configure(IAggregateResultTypeDescriptor descriptor)
    {
        descriptor.Name("BooleanAggregateResult");
        base.Configure(descriptor);
    }
}

internal sealed class GuidAggregateResultType : ComparableAggregateResultType<Guid>
{
    protected override void Configure(IAggregateResultTypeDescriptor descriptor)
    {
        descriptor.Name("UuidAggregateResult");
        base.Configure(descriptor);
    }
}

internal sealed class DateTimeAggregateResultType : ComparableAggregateResultType<DateTime>
{
    protected override void Configure(IAggregateResultTypeDescriptor descriptor)
    {
        descriptor.Name("DateTimeAggregateResult");
        base.Configure(descriptor);
    }
}

internal sealed class DateTimeOffsetAggregateResultType : ComparableAggregateResultType<DateTimeOffset>
{
    protected override void Configure(IAggregateResultTypeDescriptor descriptor)
    {
        descriptor.Name("DateTimeOffsetAggregateResult");
        base.Configure(descriptor);
    }
}

internal sealed class DateOnlyAggregateResultType : ComparableAggregateResultType<DateOnly>
{
    protected override void Configure(IAggregateResultTypeDescriptor descriptor)
    {
        descriptor.Name("DateOnlyAggregateResult");
        base.Configure(descriptor);
    }
}

internal sealed class TimeOnlyAggregateResultType : ComparableAggregateResultType<TimeOnly>
{
    protected override void Configure(IAggregateResultTypeDescriptor descriptor)
    {
        descriptor.Name("TimeOnlyAggregateResult");
        base.Configure(descriptor);
    }
}

internal sealed class TimeSpanAggregateResultType : ComparableAggregateResultType<TimeSpan>
{
    protected override void Configure(IAggregateResultTypeDescriptor descriptor)
    {
        descriptor.Name("TimeSpanAggregateResult");
        base.Configure(descriptor);
    }
}
