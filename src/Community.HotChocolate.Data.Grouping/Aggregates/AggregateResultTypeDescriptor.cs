using HotChocolate.Data.Grouping.Fields;
using HotChocolate.Internal;
using HotChocolate.Language;
using HotChocolate.Types;
using HotChocolate.Types.Descriptors;

namespace HotChocolate.Data.Grouping.Aggregates;

/// <summary>
/// Default <see cref="IAggregateResultTypeDescriptor"/>.
/// </summary>
internal sealed class AggregateResultTypeDescriptor : DescriptorBase<AggregateResultTypeConfiguration>, IAggregateResultTypeDescriptor
{
    private readonly Dictionary<GroupingAggregations, AggregateResultOperationDescriptor> _operations = [];

    private AggregateResultTypeDescriptor(IDescriptorContext context)
        : base(context)
    {
        Configuration.RuntimeType = typeof(AggregateValues);
    }

    protected override AggregateResultTypeConfiguration Configuration { get; set; } = new();

    protected override void OnCreateConfiguration(AggregateResultTypeConfiguration definition)
    {
        var allowed = Context.GetGroupingConvention(Configuration.Scope).AllowedAggregations;

        foreach (var operation in _operations.Values)
        {
            if (allowed.HasFlag(operation.Kind))
            {
                definition.Fields.Add(operation.CreateConfiguration());
            }
        }

        base.OnCreateConfiguration(definition);
    }

    /// <inheritdoc />
    public IAggregateResultTypeDescriptor Name(string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);
        Configuration.Name = value;
        Configuration.IsNamed = true;
        return this;
    }

    /// <inheritdoc />
    public IAggregateResultTypeDescriptor Description(string? value)
    {
        Configuration.Description = value;
        return this;
    }

    /// <inheritdoc />
    public IAggregateResultOperationDescriptor Operation(GroupingAggregations kind)
    {
        if (_operations.TryGetValue(kind, out var descriptor))
        {
            return descriptor;
        }

        descriptor = AggregateResultOperationDescriptor.New(Context, kind);
        _operations.Add(kind, descriptor);
        return descriptor;
    }

    /// <inheritdoc />
    public IAggregateResultTypeDescriptor Directive<TDirective>(TDirective directive) where TDirective : class
    {
        ArgumentNullException.ThrowIfNull(directive);
        Configuration.AddDirective(directive, Context.TypeInspector);
        return this;
    }

    /// <inheritdoc />
    public IAggregateResultTypeDescriptor Directive<TDirective>() where TDirective : class, new() =>
        Directive(new TDirective());

    /// <inheritdoc />
    public IAggregateResultTypeDescriptor Directive(string name, params ArgumentNode[] arguments)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        Configuration.AddDirective(new DirectiveNode(name, arguments), Context.TypeInspector);
        return this;
    }

    public static AggregateResultTypeDescriptor New(IDescriptorContext context, string? scope = null)
    {
        var d = new AggregateResultTypeDescriptor(context);
        d.Configuration.Scope = scope;
        d.Configuration.Description = context.Naming.GetTypeDescription(typeof(AggregateValues), TypeKind.Object);
        return d;
    }
}
