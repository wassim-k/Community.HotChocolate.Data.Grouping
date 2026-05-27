using HotChocolate.Data.Grouping.Fields;

namespace HotChocolate.Data.Grouping.Aggregates;

/// <summary>
/// .NET arithmetic-safety rules for aggregating a single scalar — the canonical lookup for
/// "given a source CLR type and an <see cref="AggregationKind"/>, what backing CLR type does
/// the accumulator need?". Single source of truth: per-scalar <c>*AggregateResultType</c>
/// shells call <see cref="Resolve(Type, AggregationKind)"/> to decide each operation's
/// schema-side <c>Type(...)</c>, and the queryable materialisation layer calls it to pick the
/// matching <c>Enumerable.Avg/Sum/Min/Max</c> overload. Both sides therefore agree by
/// construction.
/// </summary>
/// <remarks>
/// Widening matrix. Rows = source category, cols = aggregation kind.
/// <code>
///                 AVG       SUM       MIN/MAX
/// SmallIntegral   double?   long?     int?
/// UInt            double?   long?     long?        (uint.MaxValue exceeds int.MaxValue)
/// Long            double?   decimal?  long?
/// ULong           double?   decimal?  decimal?     (ulong.MaxValue exceeds long.MaxValue)
/// Decimal         decimal?  decimal?  decimal?
/// Float           double?   double?   double?      (float unified to double for schema parity)
/// OtherValueType  —         —         T?
/// Reference       —         —         T
/// </code>
/// </remarks>
public static class AggregateWidening
{
    public static Type Resolve(Type sourceClr, AggregationKind kind)
    {
        ArgumentNullException.ThrowIfNull(sourceClr);
        var source = Nullable.GetUnderlyingType(sourceClr) ?? sourceClr;
        var category = Categorise(source);

        return (category, kind) switch
        {
            (Category.SmallIntegral, AggregationKind.Avg) => typeof(double?),
            (Category.SmallIntegral, AggregationKind.Sum) => typeof(long?),
            (Category.SmallIntegral, AggregationKind.Min or AggregationKind.Max) => typeof(int?),

            // uint exceeds int.MaxValue, so MIN/MAX must widen to long? (not int?) to avoid overflow.
            (Category.UInt, AggregationKind.Avg) => typeof(double?),
            (Category.UInt, AggregationKind.Sum) => typeof(long?),
            (Category.UInt, AggregationKind.Min or AggregationKind.Max) => typeof(long?),

            (Category.Long, AggregationKind.Avg) => typeof(double?),
            (Category.Long, AggregationKind.Sum) => typeof(decimal?),
            (Category.Long, AggregationKind.Min or AggregationKind.Max) => typeof(long?),

            (Category.ULong, AggregationKind.Avg) => typeof(double?),
            (Category.ULong, AggregationKind.Sum) => typeof(decimal?),
            (Category.ULong, AggregationKind.Min or AggregationKind.Max) => typeof(decimal?),

            (Category.Decimal, _) => typeof(decimal?),

            (Category.Float, _) => typeof(double?),

            (Category.OtherValueType, AggregationKind.Min or AggregationKind.Max)
                => typeof(Nullable<>).MakeGenericType(source),
            (Category.Reference, AggregationKind.Min or AggregationKind.Max) => source,

            _ => throw new InvalidOperationException(
                $"Cannot widen {source} for {kind}: only MIN/MAX are defined for {category}."),
        };
    }

    private enum Category { SmallIntegral, UInt, Long, ULong, Decimal, Float, OtherValueType, Reference }

    private static Category Categorise(Type t) => Type.GetTypeCode(t) switch
    {
        TypeCode.Byte or TypeCode.SByte
            or TypeCode.Int16 or TypeCode.UInt16
            or TypeCode.Int32 => Category.SmallIntegral,
        TypeCode.UInt32 => Category.UInt,
        TypeCode.Int64 => Category.Long,
        TypeCode.UInt64 => Category.ULong,
        TypeCode.Decimal => Category.Decimal,
        TypeCode.Single or TypeCode.Double => Category.Float,
        _ => t.IsValueType ? Category.OtherValueType : Category.Reference,
    };
}
