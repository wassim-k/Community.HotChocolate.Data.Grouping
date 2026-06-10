using HotChocolate.Data.Grouping.Fields;
using HotChocolate.Language;
using HotChocolate.Types;

namespace HotChocolate.Data.Grouping.Aggregates;

/// <summary>
/// Fluent surface for an <see cref="AggregateResultType"/>.
/// </summary>
public interface IAggregateResultTypeDescriptor : IDescriptor<AggregateResultTypeConfiguration>, IFluent
{
    IAggregateResultTypeDescriptor Name(string value);

    IAggregateResultTypeDescriptor Description(string? value);

    IAggregateResultOperationDescriptor Operation(GroupingAggregations kind);

    IAggregateResultTypeDescriptor Directive<TDirective>(TDirective directive) where TDirective : class;

    IAggregateResultTypeDescriptor Directive<TDirective>() where TDirective : class, new();

    IAggregateResultTypeDescriptor Directive(string name, params ArgumentNode[] arguments);
}
