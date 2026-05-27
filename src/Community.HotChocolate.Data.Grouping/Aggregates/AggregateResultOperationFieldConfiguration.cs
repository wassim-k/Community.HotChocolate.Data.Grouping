using System.ComponentModel;
using HotChocolate.Data.Grouping.Fields;
using HotChocolate.Types.Descriptors.Configurations;

namespace HotChocolate.Data.Grouping.Aggregates;

/// <summary>
/// Configuration produced by the aggregate-result operation descriptor. Public only so it can
/// satisfy <see cref="IAggregateResultOperationDescriptor"/>'s <c>IDescriptor&lt;T&gt;</c>
/// constraint; consumers should not construct or read it directly.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public class AggregateResultOperationFieldConfiguration : ObjectFieldConfiguration
{
    public AggregationKind Kind { get; set; }
}
