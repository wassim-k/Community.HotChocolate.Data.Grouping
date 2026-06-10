using HotChocolate.Data.Grouping.Fields;
using HotChocolate.Data.Grouping.Naming;
using HotChocolate.Internal;
using HotChocolate.Language;
using HotChocolate.Resolvers;
using HotChocolate.Types;
using HotChocolate.Types.Descriptors;
using HotChocolate.Types.Descriptors.Configurations;

namespace HotChocolate.Data.Grouping.Aggregates;

/// <summary>
/// Default <see cref="IAggregateResultOperationDescriptor"/>.
/// </summary>
internal sealed class AggregateResultOperationDescriptor : DescriptorBase<AggregateResultOperationFieldConfiguration>, IAggregateResultOperationDescriptor
{
    private AggregateResultOperationDescriptor(IDescriptorContext context, GroupingAggregations kind)
        : base(context)
    {
        Configuration.Kind = kind;
        Configuration.Name = GroupingFieldNames.For(kind);
        Configuration.PureResolver = GetResolver(kind);
    }

    protected override AggregateResultOperationFieldConfiguration Configuration { get; set; } = new();

    public GroupingAggregations Kind => Configuration.Kind;

    /// <inheritdoc />
    public IAggregateResultOperationDescriptor Name(string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);
        Configuration.Name = value;
        return this;
    }

    /// <inheritdoc />
    public IAggregateResultOperationDescriptor Description(string? value)
    {
        Configuration.Description = value;
        return this;
    }

    /// <inheritdoc />
    public IAggregateResultOperationDescriptor Type<TOutputType>() where TOutputType : IOutputType
    {
        Configuration.Type = Context.TypeInspector.GetTypeRef(typeof(TOutputType), TypeContext.Output);
        return this;
    }

    /// <inheritdoc />
    public IAggregateResultOperationDescriptor Type(ITypeNode typeNode)
    {
        ArgumentNullException.ThrowIfNull(typeNode);
        Configuration.Type = TypeReference.Create(typeNode, TypeContext.Output);
        return this;
    }

    /// <inheritdoc />
    public IAggregateResultOperationDescriptor Type(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        Configuration.Type = Context.TypeInspector.GetTypeRef(type, TypeContext.Output);
        return this;
    }

    /// <inheritdoc />
    public IAggregateResultOperationDescriptor Having<TRuntime>() => Having(typeof(TRuntime));

    /// <inheritdoc />
    public IAggregateResultOperationDescriptor Having(Type runtimeType)
    {
        ArgumentNullException.ThrowIfNull(runtimeType);

        // Resolve immediately: the filter convention completes during schema-builder setup,
        // before type discovery drives our descriptors, so GetFieldType is ready by the time
        // this runs. Resolving here (rather than in an OnComplete task) ensures the returned
        // type reference participates in type discovery and the filter input gets included
        // in the schema. Returns null (and we no-op) when filtering isn't registered.
        var filterTypeRef = Context.GetFilterTypeRef(runtimeType);
        if (filterTypeRef is null)
        {
            return this;
        }

        var existing = Configuration.Arguments.FirstOrDefault(a => a.Name == GroupingArgumentNames.Having);
        if (existing is not null)
        {
            Configuration.Arguments.Remove(existing);
        }

        Configuration.Arguments.Add(new ArgumentConfiguration
        {
            Name = GroupingArgumentNames.Having,
            Type = filterTypeRef,
        });
        return this;
    }

    /// <inheritdoc />
    public IAggregateResultOperationDescriptor Ignore(bool ignore = true)
    {
        Configuration.Ignore = ignore;
        return this;
    }

    /// <inheritdoc />
    public IAggregateResultOperationDescriptor Directive<TDirective>(TDirective directiveInstance) where TDirective : class
    {
        ArgumentNullException.ThrowIfNull(directiveInstance);
        Configuration.AddDirective(directiveInstance, Context.TypeInspector);
        return this;
    }

    /// <inheritdoc />
    public IAggregateResultOperationDescriptor Directive<TDirective>() where TDirective : class, new() =>
        Directive(new TDirective());

    /// <inheritdoc />
    public IAggregateResultOperationDescriptor Directive(string name, params ArgumentNode[] arguments)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        Configuration.AddDirective(new DirectiveNode(name, arguments), Context.TypeInspector);
        return this;
    }

    private static PureFieldDelegate GetResolver(GroupingAggregations kind) => kind switch
    {
        GroupingAggregations.Avg => static ctx => ctx.Parent<AggregateValues>().Avg,
        GroupingAggregations.Sum => static ctx => ctx.Parent<AggregateValues>().Sum,
        GroupingAggregations.Min => static ctx => ctx.Parent<AggregateValues>().Min,
        GroupingAggregations.Max => static ctx => ctx.Parent<AggregateValues>().Max,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    public static AggregateResultOperationDescriptor New(IDescriptorContext context, GroupingAggregations kind) =>
        new(context, kind);
}
