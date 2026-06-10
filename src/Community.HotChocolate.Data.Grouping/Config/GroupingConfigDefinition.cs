using System.Reflection;

namespace HotChocolate.Data.Grouping.Config;

internal sealed class GroupingConfigDefinition(
    IReadOnlyDictionary<MemberInfo, string> renames,
    IReadOnlySet<MemberInfo> ignored,
    IReadOnlyDictionary<MemberInfo, IReadOnlyList<object>> directives)
{
    public IReadOnlyDictionary<MemberInfo, string> Renames { get; } = renames;

    public IReadOnlySet<MemberInfo> Ignored { get; } = ignored;

    public IReadOnlyDictionary<MemberInfo, IReadOnlyList<object>> Directives { get; } = directives;
}
