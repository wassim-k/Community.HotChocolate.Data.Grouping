using System.ComponentModel;
using System.Reflection;
using HotChocolate.Data.Grouping.Fields;

namespace HotChocolate.Data.Grouping;

/// <summary>
/// Fluent builder for a single <see cref="GroupingResult{T}"/>.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class GroupingResultBuilder<T>
{
    private readonly Dictionary<PropertyInfo, object?> _key = [];
    private readonly Dictionary<PropertyInfo, object?> _aggregates = [];
    private int _count;

    public GroupingResultBuilder<T> SetKey(PathSegment[] path, object? value)
    {
        Insert(_key, path, value);
        return this;
    }

    public GroupingResultBuilder<T> SetCount(int count)
    {
        _count = count;
        return this;
    }

    /// <summary>
    /// Sets one operation slot of a leaf's aggregate values.
    /// </summary>
    /// <remarks>
    /// Runtime type of <paramref name="value"/> matches the schema's declared return per scalar
    /// (e.g. <c>decimal?</c> for decimal AVG/SUM; <c>double?</c> for double/float or integral AVG;
    /// <c>long?</c> for small-integral SUM; source scalar's nullable type for MIN/MAX).
    /// </remarks>
    public GroupingResultBuilder<T> SetAggregate(PathSegment[] path, GroupingAggregations kind, object? value)
    {
        var leaf = EnsureLeafSlot(_aggregates, path);
        switch (kind)
        {
            case GroupingAggregations.Avg: leaf.Avg = value; break;
            case GroupingAggregations.Sum: leaf.Sum = value; break;
            case GroupingAggregations.Min: leaf.Min = value; break;
            case GroupingAggregations.Max: leaf.Max = value; break;
            default: throw ThrowHelper.Grouping_UnknownGroupingAggregations();
        }
        return this;
    }

    public GroupingResult<T> Build() => new()
    {
        Key = Wrap(_key),
        Count = _count,
        Aggregate = Wrap(_aggregates),
    };

    private static Dictionary<PropertyInfo, object?> DescendToLeafParent(
        Dictionary<PropertyInfo, object?> root,
        PathSegment[] path)
    {
        var current = root;
        for (var depth = 0; depth < path.Length - 1; depth++)
        {
            var key = path[depth].Property;
            if (!current.TryGetValue(key, out var existing)
                || existing is not Dictionary<PropertyInfo, object?> nested)
            {
                nested = [];
                current[key] = nested;
            }
            current = nested;
        }
        return current;
    }

    private static void Insert(Dictionary<PropertyInfo, object?> root, PathSegment[] path, object? value)
        => DescendToLeafParent(root, path)[path[^1].Property] = value;

    private static AggregateSlot EnsureLeafSlot(Dictionary<PropertyInfo, object?> root, PathSegment[] path)
    {
        var parent = DescendToLeafParent(root, path);
        var leafKey = path[^1].Property;
        if (!parent.TryGetValue(leafKey, out var slot) || slot is not AggregateSlot existingSlot)
        {
            existingSlot = new AggregateSlot();
            parent[leafKey] = existingSlot;
        }
        return existingSlot;
    }

    private static GroupingFields Wrap(Dictionary<PropertyInfo, object?> source)
    {
        var wrapped = new Dictionary<PropertyInfo, object?>(source.Count);
        foreach (var (key, value) in source)
        {
            wrapped[key] = value switch
            {
                Dictionary<PropertyInfo, object?> nested => Wrap(nested),
                AggregateSlot slot => slot.ToValues(),
                _ => value,
            };
        }
        return new GroupingFields(wrapped);
    }

    private sealed class AggregateSlot
    {
        public object? Avg;
        public object? Sum;
        public object? Min;
        public object? Max;
        public AggregateValues ToValues() => new()
        {
            Avg = Avg,
            Sum = Sum,
            Min = Min,
            Max = Max,
        };
    }
}
