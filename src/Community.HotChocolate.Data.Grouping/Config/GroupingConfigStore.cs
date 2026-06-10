using System.Collections.Concurrent;

namespace HotChocolate.Data.Grouping.Config;

internal sealed class GroupingConfigStore
{
    private readonly ConcurrentDictionary<Type, GroupingConfigDefinition> _configs = new();

    public GroupingConfigStore(IEnumerable<GroupingConfig> explicitConfigs)
    {
        foreach (var config in explicitConfigs)
        {
            _configs.TryAdd(config.EntityType, config.CreateDefinition());
        }
    }

    public GroupingConfigDefinition? Get(Type entityType) =>
        _configs.TryGetValue(entityType, out var config) ? config : null;
}
