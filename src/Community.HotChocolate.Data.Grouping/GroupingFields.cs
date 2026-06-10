using System.ComponentModel;
using System.Reflection;

namespace HotChocolate.Data.Grouping;

/// <summary>
/// One level of a key or aggregate tree.
/// </summary>
/// <remarks>A missing entry means the property wasn't selected.</remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class GroupingFields(IReadOnlyDictionary<PropertyInfo, object?> entries)
{
    public IReadOnlyDictionary<PropertyInfo, object?> Entries { get; } = entries;
}
