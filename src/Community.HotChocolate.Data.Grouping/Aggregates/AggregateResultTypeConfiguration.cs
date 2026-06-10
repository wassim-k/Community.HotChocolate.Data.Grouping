using System.ComponentModel;
using HotChocolate.Types.Descriptors.Configurations;

namespace HotChocolate.Data.Grouping.Aggregates;

/// <summary>
/// Configuration produced by the aggregate-result type descriptor. Public only so it can
/// satisfy <see cref="IAggregateResultTypeDescriptor"/>'s <c>IDescriptor&lt;T&gt;</c> constraint;
/// consumers should not construct or read it directly.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public class AggregateResultTypeConfiguration : ObjectTypeConfiguration
{
    public string? Scope { get; set; }

    public bool IsNamed { get; set; }
}
