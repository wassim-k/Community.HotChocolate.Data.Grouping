using HotChocolate.Language;

namespace HotChocolate.Data.Grouping.Config;

internal sealed class GroupingFieldDescriptor : IGroupingFieldDescriptor
{
    private List<object>? _directives;

    public string? OverrideName { get; private set; }

    public bool IsIgnored { get; private set; }

    public IReadOnlyList<object> Directives => (IReadOnlyList<object>?)_directives ?? [];

    public IGroupingFieldDescriptor Name(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        OverrideName = name;
        return this;
    }

    public IGroupingFieldDescriptor Ignore(bool ignore = true)
    {
        IsIgnored = ignore;
        return this;
    }

    public IGroupingFieldDescriptor Directive<TDirective>(TDirective directiveInstance)
        where TDirective : class
    {
        ArgumentNullException.ThrowIfNull(directiveInstance);
        _directives ??= [];
        _directives.Add(directiveInstance);
        return this;
    }

    public IGroupingFieldDescriptor Directive<TDirective>()
        where TDirective : class, new() =>
        Directive(new TDirective());

    public IGroupingFieldDescriptor Directive(string name, params ArgumentNode[] arguments)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        _directives ??= [];
        _directives.Add(new DirectiveNode(name, arguments));
        return this;
    }
}
