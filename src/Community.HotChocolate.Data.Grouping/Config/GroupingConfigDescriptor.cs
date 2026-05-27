using System.Linq.Expressions;
using System.Reflection;

namespace HotChocolate.Data.Grouping.Config;

internal sealed class GroupingConfigDescriptor<T> : IGroupingConfigDescriptor<T>
{
    private readonly Dictionary<MemberInfo, GroupingFieldDescriptor> _fields = new();

    public IGroupingFieldDescriptor Field(Expression<Func<T, object?>> propertyOrMethod)
    {
        var member = ExtractMember(propertyOrMethod);
        if (!_fields.TryGetValue(member, out var descriptor))
        {
            descriptor = new GroupingFieldDescriptor();
            _fields[member] = descriptor;
        }

        return descriptor;
    }

    public GroupingConfigDefinition CreateDefinition()
    {
        var renames = new Dictionary<MemberInfo, string>();
        var ignored = new HashSet<MemberInfo>();
        var directives = new Dictionary<MemberInfo, IReadOnlyList<object>>();

        foreach (var (member, descriptor) in _fields)
        {
            if (descriptor.OverrideName is { } name)
            {
                renames[member] = name;
            }

            if (descriptor.IsIgnored)
            {
                ignored.Add(member);
            }

            if (descriptor.Directives.Count > 0)
            {
                directives[member] = descriptor.Directives;
            }
        }

        return new GroupingConfigDefinition(renames, ignored, directives);
    }

    private static MemberInfo ExtractMember(Expression<Func<T, object?>> expression)
    {
        var body = expression.Body;
        if (body is UnaryExpression { NodeType: ExpressionType.Convert } unary)
        {
            body = unary.Operand;
        }

        return body switch
        {
            MemberExpression me => me.Member,
            MethodCallExpression mc => mc.Method,
            _ => throw new ArgumentException(
                $"Expression '{expression}' does not refer to a property or method.",
                nameof(expression)),
        };
    }
}
