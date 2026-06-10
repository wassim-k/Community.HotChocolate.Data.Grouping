using System.Linq.Expressions;

namespace HotChocolate.Data.Grouping.Config;

/// <summary>
/// Per-entity grouping configuration — rename fields, hide them, or attach directives
/// across every generated schema type.
/// </summary>
public interface IGroupingConfigDescriptor<T>
{
    /// <summary>Targets a field on the entity for further configuration.</summary>
    IGroupingFieldDescriptor Field(Expression<Func<T, object?>> propertyOrMethod);
}
