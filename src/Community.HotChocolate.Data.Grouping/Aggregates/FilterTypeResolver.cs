using System.Diagnostics.CodeAnalysis;
using HotChocolate.Data.Filters;
using HotChocolate.Types.Descriptors;

namespace HotChocolate.Data.Grouping.Aggregates;

/// <summary>
/// Bridges to HotChocolate.Data's <see cref="IFilterConvention"/> for grouping HAVING resolution.
/// HAVING is opt-in: when <c>AddFiltering(...)</c> isn't registered, both helpers silently
/// no-op so schema build succeeds without <c>having:</c> arguments.
/// </summary>
/// <remarks>
/// <see cref="GetFilterTypeRef"/> resolves a runtime CLR type to the <c>FilterInputType</c> bound for
/// it on the convention — consumer customizations via
/// <c>AddFiltering(f =&gt; f.BindRuntimeType&lt;TRuntime, TFilter&gt;())</c> flow through automatically.
/// Uses HotChocolate.Data's public <see cref="IFilterConvention.GetFieldType"/> API the same way
/// HotChocolate.Data itself does (see <c>FilterFieldDescriptor</c>): a raw <see cref="Type"/> is a
/// <see cref="System.Reflection.MemberInfo"/>, and HotChocolate's <c>ExtendedType.FromMember</c>
/// routes <c>Type</c> values through <c>FromType</c>. Safe to call at descriptor-configure /
/// type-discovery time — HotChocolate completes the filter convention during schema-builder setup,
/// before our types are discovered. Resolving inline (rather than deferring) is essential so the
/// returned type reference participates in the dependency graph at discovery.
/// </remarks>
internal static class FilterTypeResolver
{
    public static bool IsFilterAvailable(IDescriptorContext context, string? scope = null) =>
        TryGetConvention(context, scope, out _);

    public static ExtendedTypeReference? GetFilterTypeRef(
        this IDescriptorContext context,
        Type runtimeType,
        string? scope = null)
    {
        if (!TryGetConvention(context, scope, out var convention))
        {
            return null;
        }

        try
        {
            return convention.GetFieldType(runtimeType);
        }
        catch (SchemaException)
        {
            return null;
        }
    }

    private static bool TryGetConvention(
        IDescriptorContext context,
        string? scope,
        [NotNullWhen(true)] out IFilterConvention? convention)
    {
        try
        {
            convention = context.GetConventionOrDefault<IFilterConvention>(static () => null!, scope);
            return convention is not null;
        }
        catch (SchemaException)
        {
            convention = null;
            return false;
        }
    }
}
