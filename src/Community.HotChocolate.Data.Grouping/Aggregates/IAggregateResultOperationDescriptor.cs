using HotChocolate.Language;
using HotChocolate.Types;

namespace HotChocolate.Data.Grouping.Aggregates;

/// <summary>
/// Fluent surface for a single aggregate-result field (avg/sum/min/max).
/// </summary>
public interface IAggregateResultOperationDescriptor : IDescriptor<AggregateResultOperationFieldConfiguration>, IFluent
{
    IAggregateResultOperationDescriptor Name(string value);

    IAggregateResultOperationDescriptor Description(string? value);

    IAggregateResultOperationDescriptor Type<TOutputType>() where TOutputType : IOutputType;

    IAggregateResultOperationDescriptor Type(ITypeNode typeNode);

    IAggregateResultOperationDescriptor Type(Type type);

    IAggregateResultOperationDescriptor Having(Type runtimeType);

    IAggregateResultOperationDescriptor Having<TRuntime>();

    IAggregateResultOperationDescriptor Ignore(bool ignore = true);

    IAggregateResultOperationDescriptor Directive<TDirective>(TDirective directive) where TDirective : class;

    IAggregateResultOperationDescriptor Directive<TDirective>() where TDirective : class, new();

    IAggregateResultOperationDescriptor Directive(string name, params ArgumentNode[] arguments);
}
