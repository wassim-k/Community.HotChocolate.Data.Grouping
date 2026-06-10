#pragma warning disable IDE0130 // Namespace does not match folder structure

using System.Reflection;
using System.Runtime.CompilerServices;
using HotChocolate.Data.Grouping.Extensions;
using HotChocolate.Types;
using HotChocolate.Types.Descriptors;

namespace HotChocolate.Data.Grouping;

/// <summary>
/// Opts a resolver field into grouping.
/// </summary>
[AttributeUsage(
    AttributeTargets.Property | AttributeTargets.Method,
    Inherited = true,
    AllowMultiple = false)]
public sealed class UseGroupingAttribute : ObjectFieldDescriptorAttribute
{
    public UseGroupingAttribute([CallerLineNumber] int order = 0)
    {
        Order = order;
    }

    /// <summary>Convention scope; <see langword="null"/> for the unscoped default.</summary>
    public string? Scope { get; set; }

    /// <inheritdoc />
    protected override void OnConfigure(
        IDescriptorContext context,
        IObjectFieldDescriptor descriptor,
        MemberInfo? member)
    {
        descriptor.UseGrouping(Scope);
    }
}
