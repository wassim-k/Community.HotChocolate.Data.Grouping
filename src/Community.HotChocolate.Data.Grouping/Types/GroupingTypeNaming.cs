using HotChocolate.Configuration;
using HotChocolate.Types;

namespace HotChocolate.Data.Grouping.Types;

internal static class GroupingTypeNaming
{
    // An entity resolving to any of these names would silently collide with the wrapper type.
    public static readonly IReadOnlySet<string> ReservedEntityNames = new HashSet<string>(
        ["Grouping", "GroupingKey", "Aggregate"],
        StringComparer.Ordinal);

    public static void EnsureEntityNameNotReserved(string name)
    {
        if (ReservedEntityNames.Contains(name))
        {
            throw ThrowHelper.Grouping_ReservedEntityName(name);
        }
    }

    public static string ResolveEntityName(ITypeCompletionContext context, Type entityType)
    {
        var typeRef = context.TypeInspector.GetTypeRef(entityType, TypeContext.Output);
        var name = context.TryGetType<ITypeDefinition>(typeRef, out var typeDef)
            ? typeDef.Name
            : context.DescriptorContext.Naming.GetTypeName(entityType, TypeKind.Object);
        EnsureEntityNameNotReserved(name);
        return name;
    }
}
