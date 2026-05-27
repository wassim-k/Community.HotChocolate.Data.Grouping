using HotChocolate.Language;
using HotChocolate.Types;

namespace HotChocolate.Data.Grouping.Config;

/// <summary>
/// Per-field configuration on a <see cref="GroupingConfig{T}"/>.
/// </summary>
public interface IGroupingFieldDescriptor : IFluent
{
    /// <summary>Renames the field across every grouping schema type for the entity.</summary>
    IGroupingFieldDescriptor Name(string name);

    /// <summary>Drops the field from every grouping schema type for the entity.</summary>
    IGroupingFieldDescriptor Ignore(bool ignore = true);

    /// <summary>Attaches a directive runtime instance to the field.</summary>
    IGroupingFieldDescriptor Directive<TDirective>(TDirective directiveInstance)
        where TDirective : class;

    /// <summary>Attaches a directive using its parameterless constructor.</summary>
    IGroupingFieldDescriptor Directive<TDirective>()
        where TDirective : class, new();

    /// <summary>Attaches a directive by GraphQL name and AST argument nodes.</summary>
    IGroupingFieldDescriptor Directive(string name, params ArgumentNode[] arguments);
}
