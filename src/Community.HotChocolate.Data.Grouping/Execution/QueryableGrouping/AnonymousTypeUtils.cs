using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace HotChocolate.Data.Grouping.Execution.QueryableGrouping;

// Pre-defined AnonymousType<...> closed generics (arity 1..16) so EF Core and
// MongoDB.Driver.Linq translate them like compiler-emitted anonymous types.
internal static class AnonymousTypeUtils
{
    public const int MaxArity = 16;

    private static readonly ConcurrentDictionary<(Type CarrierType, int Index), CarrierSlot> _cache = new();

    public static Type Create(IReadOnlyList<Type> types)
    {
        ArgumentOutOfRangeException.ThrowIfZero(types.Count);

        // Surfaced as GROUPING_NOT_SUPPORTED by the middleware rather than an opaque arity error.
        if (types.Count > MaxArity)
        {
            throw new NotSupportedException(
                $"This grouping selection needs {types.Count} carrier slots but at most {MaxArity} are "
                + "supported. Reduce the number of aggregates, key paths, or nested collections in the selection.");
        }

        return OpenDefinition(types.Count).MakeGenericType([.. types]);
    }

    public static Expression New(Type carrierType, IReadOnlyList<Expression> values)
    {
        var constructor = carrierType.GetConstructors().Single();
        var members = new MemberInfo[values.Count];
        for (var i = 0; i < values.Count; i++)
        {
            members[i] = ItemProperty(carrierType, i);
        }

        return Expression.New(constructor, values, members);
    }

    public static CarrierSlot[] ItemProperties(Type carrierType, int count)
    {
        var slots = new CarrierSlot[count];
        for (var i = 0; i < count; i++)
        {
            slots[i] = _cache.GetOrAdd(
                (carrierType, i),
                static key => new CarrierSlot(ItemProperty(key.CarrierType, key.Index)));
        }
        return slots;
    }

    private static PropertyInfo ItemProperty(Type carrierType, int index)
    {
        var name = $"Item{index + 1}";
        return carrierType.GetProperty(name)
            ?? throw new InvalidOperationException($"Type '{carrierType}' does not expose property '{name}'.");
    }

    private static Type OpenDefinition(int arity) => arity switch
    {
        1 => typeof(AnonymousType<>),
        2 => typeof(AnonymousType<,>),
        3 => typeof(AnonymousType<,,>),
        4 => typeof(AnonymousType<,,,>),
        5 => typeof(AnonymousType<,,,,>),
        6 => typeof(AnonymousType<,,,,,>),
        7 => typeof(AnonymousType<,,,,,,>),
        8 => typeof(AnonymousType<,,,,,,,>),
        9 => typeof(AnonymousType<,,,,,,,,>),
        10 => typeof(AnonymousType<,,,,,,,,,>),
        11 => typeof(AnonymousType<,,,,,,,,,,>),
        12 => typeof(AnonymousType<,,,,,,,,,,,>),
        13 => typeof(AnonymousType<,,,,,,,,,,,,>),
        14 => typeof(AnonymousType<,,,,,,,,,,,,,>),
        15 => typeof(AnonymousType<,,,,,,,,,,,,,,>),
        16 => typeof(AnonymousType<,,,,,,,,,,,,,,,>),
        _ => throw new ArgumentOutOfRangeException(nameof(arity), arity, $"Arity must be 1..{MaxArity}."),
    };
}

internal sealed class CarrierSlot(PropertyInfo property)
{
    public Type Type => property.PropertyType;

    public object? GetValue(object? instance) =>
        instance is null ? null : property.GetValue(instance);

    public Expression Access(Expression target) => Expression.Property(target, property);
}
