using System.Collections.Concurrent;
using System.Reflection;
using HotChocolate.Configuration;
using HotChocolate.Data.Grouping.Config;
using HotChocolate.Internal;
using HotChocolate.Types.Descriptors;
using HotChocolate.Types.Descriptors.Configurations;

namespace HotChocolate.Data.Grouping.Types;

internal sealed class GroupingConfigInterceptor(GroupingConfigStore store) : TypeInterceptor
{
    private static readonly HashSet<Type> _groupingOpenGenerics =
    [
        typeof(GroupingKeyType<>),
        typeof(GroupingAggregateType<>),
    ];

    private readonly GroupingConfigStore _store = store;
    private readonly ConcurrentDictionary<Type, ObjectTypeConfiguration> _entityConfigurations = new();

    public override void OnAfterRegisterDependencies(
        ITypeDiscoveryContext discoveryContext,
        TypeSystemConfiguration configuration)
    {
        if (configuration is not ObjectTypeConfiguration objConfig
            || objConfig.RuntimeType is not { } runtimeType
            || runtimeType == typeof(object)
            || IsGroupingOpenGeneric(discoveryContext.Type.GetType()))
        {
            return;
        }

        _entityConfigurations[runtimeType] = objConfig;
    }

    public override void OnBeforeCompleteType(
        ITypeCompletionContext completionContext,
        TypeSystemConfiguration configuration)
    {
        if (configuration is not ObjectTypeConfiguration objConfig
            || !IsGroupingOpenGeneric(completionContext.Type.GetType()))
        {
            return;
        }

        var entityType = completionContext.Type.GetType().GenericTypeArguments[0];

        if (_store.Get(entityType) is { } explicitConfig)
        {
            ApplyExplicitConfig(objConfig, explicitConfig, completionContext.DescriptorContext.TypeInspector);
            return;
        }

        if (!_entityConfigurations.TryGetValue(entityType, out var entityConfig))
        {
            return;
        }

        ApplyInheritedConfig(objConfig, entityConfig);
    }

    private static void ApplyExplicitConfig(
        ObjectTypeConfiguration objConfig,
        GroupingConfigDefinition config,
        ITypeInspector typeInspector)
    {
        ApplyRenames(objConfig, config.Renames);
        ApplyDirectives(objConfig, config.Directives, typeInspector);

        if (config.Ignored.Count == 0)
        {
            return;
        }

        for (var i = objConfig.Fields.Count - 1; i >= 0; i--)
        {
            if (objConfig.Fields[i].Member is { } member && config.Ignored.Contains(member))
            {
                objConfig.Fields.RemoveAt(i);
            }
        }
    }

    private static void ApplyDirectives(
        ObjectTypeConfiguration objConfig,
        IReadOnlyDictionary<MemberInfo, IReadOnlyList<object>> directives,
        ITypeInspector typeInspector)
    {
        if (directives.Count == 0)
        {
            return;
        }

        foreach (var field in objConfig.Fields)
        {
            if (field.Member is { } member && directives.TryGetValue(member, out var fieldDirectives))
            {
                foreach (var directive in fieldDirectives)
                {
                    field.AddDirective(directive, typeInspector);
                }
            }
        }
    }

    private static void ApplyInheritedConfig(
        ObjectTypeConfiguration objConfig,
        ObjectTypeConfiguration entityConfig)
    {
        Dictionary<MemberInfo, string>? renames = null;
        foreach (var entityField in entityConfig.Fields)
        {
            if (entityField.Member is { } member)
            {
                renames ??= [];
                renames[member] = entityField.Name;
            }
        }

        if (renames is not null)
        {
            ApplyRenames(objConfig, renames);
        }

        var ignoredBindings = entityConfig.FieldIgnores;
        if (ignoredBindings.Count == 0)
        {
            return;
        }

        var ignored = new HashSet<string>(ignoredBindings.Count, StringComparer.Ordinal);
        foreach (var binding in ignoredBindings)
        {
            ignored.Add(binding.Name);
        }

        for (var i = objConfig.Fields.Count - 1; i >= 0; i--)
        {
            if (ignored.Contains(objConfig.Fields[i].Name))
            {
                objConfig.Fields.RemoveAt(i);
            }
        }
    }

    private static void ApplyRenames(
        ObjectTypeConfiguration objConfig,
        IReadOnlyDictionary<MemberInfo, string> renames)
    {
        if (renames.Count == 0)
        {
            return;
        }

        foreach (var field in objConfig.Fields)
        {
            if (field.Member is { } member
                && renames.TryGetValue(member, out var renamedName)
                && field.Name != renamedName)
            {
                field.Name = renamedName;
            }
        }
    }

    private static bool IsGroupingOpenGeneric(Type runtimeType) =>
        runtimeType.IsGenericType && _groupingOpenGenerics.Contains(runtimeType.GetGenericTypeDefinition());
}
