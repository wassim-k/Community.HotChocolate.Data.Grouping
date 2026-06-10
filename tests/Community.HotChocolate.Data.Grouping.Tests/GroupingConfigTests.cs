using HotChocolate.Data.Grouping.Config;
using HotChocolate.Execution;
using HotChocolate.Types;
using Microsoft.Extensions.DependencyInjection;

namespace HotChocolate.Data.Grouping;

public class GroupingConfigTests
{
    [Fact]
    public async Task WithoutObjectType_AppliesRenames()
    {
        var services = new ServiceCollection();
        services
            .AddGraphQL()
            .AddGrouping()
            .AddGroupingConfig<TestConfigWithoutObjectType>()
            .AddQueryType<TestQueryNoObjectType>();

        await using var provider = services.BuildServiceProvider();
        var executor = await provider.GetRequestExecutorAsync();
        var schema = executor.Schema;

        var schemaString = schema.ToString();
        Assert.Contains("entityName", schemaString, StringComparison.Ordinal);

        var groupKeyDef = schema.Types.OfType<IObjectTypeDefinition>()
            .FirstOrDefault(t => t.Fields.Any(f => f.Name == "entityName"));
        Assert.NotNull(groupKeyDef);
    }

    [Fact]
    public async Task WithObjectType_TakesPriority()
    {
        var services = new ServiceCollection();
        services
            .AddGraphQL()
            .AddGrouping()
            .AddGroupingConfig<TestConfigWithObjectType>()
            .AddType<RenamedTestEntity>()
            .AddQueryType<TestQueryWithObjectType>();

        await using var provider = services.BuildServiceProvider();
        var executor = await provider.GetRequestExecutorAsync();
        var schema = executor.Schema;

        var groupingTypes = schema.Types.OfType<IObjectTypeDefinition>()
            .Where(t => t.Name.Contains("Grouping", StringComparison.Ordinal))
            .ToArray();

        Assert.Contains(groupingTypes, t => t.Fields.Any(f => f.Name == "entityName"));
        Assert.DoesNotContain(groupingTypes, t => t.Fields.Any(f => f.Name == "theName"));
    }

    [Fact]
    public async Task ConstructorInjection_ResolvesFromSchemaServices()
    {
        // Schema-service-registered feature gate flips behaviour in the config's
        // Configure body. Verifies that AddGroupingConfig<T>() activates the config
        // through ActivatorUtilities and resolves ctor dependencies from schema services.
        var services = new ServiceCollection();
        services
            .AddGraphQL()
            .AddGrouping()
            .ConfigureSchemaServices(s => s.AddSingleton<IFakeFeatureGate>(new FakeFeatureGate(hideValue: true)))
            .AddGroupingConfig<TestConfigWithInjectedGate>()
            .AddQueryType<TestQueryNoObjectType>();

        await using var provider = services.BuildServiceProvider();
        var executor = await provider.GetRequestExecutorAsync();
        var schema = executor.Schema;

        // The injected gate told the config to Ignore() the Value field on the entity,
        // so TestEntityAggregate should not expose a "value" field.
        var aggregate = schema.Types.GetType<IObjectTypeDefinition>("TestEntityAggregate");
        Assert.DoesNotContain(aggregate.Fields, f => f.Name == "value");
    }

    public interface IFakeFeatureGate
    {
        bool HideValue { get; }
    }

    private sealed class FakeFeatureGate(bool hideValue) : IFakeFeatureGate
    {
        public bool HideValue { get; } = hideValue;
    }

    private sealed class TestConfigWithInjectedGate(IFakeFeatureGate gate) : GroupingConfig<TestEntity>
    {
        protected override void Configure(IGroupingConfigDescriptor<TestEntity> descriptor)
        {
            if (gate.HideValue)
            {
                descriptor.Field(e => e.Value).Ignore();
            }
        }
    }

    private sealed class TestConfigWithoutObjectType : GroupingConfig<TestEntity>
    {
        protected override void Configure(IGroupingConfigDescriptor<TestEntity> descriptor)
        {
            descriptor
                .Field(e => e.Name)
                .Name("entityName");
        }
    }

    private sealed class TestConfigWithObjectType : GroupingConfig<TestEntity>
    {
        protected override void Configure(IGroupingConfigDescriptor<TestEntity> descriptor)
        {
            descriptor
                .Field(e => e.Name)
                .Name("entityName");
        }
    }

    private sealed class RenamedTestEntity : ObjectType<TestEntity>
    {
        protected override void Configure(IObjectTypeDescriptor<TestEntity> descriptor)
        {
            descriptor
                .Field(e => e.Name)
                .Name("theName");
        }
    }

    public record TestEntity
    {
        public int Id { get; set; }

        public string Name { get; set; } = default!;

        public decimal Value { get; set; }
    }

    public class TestQueryNoObjectType
    {
        [UseGrouping]
        public IQueryable<TestEntity> GetTestEntities() =>
            new[] { new TestEntity { Id = 1, Name = "Test", Value = 100 } }.AsQueryable();
    }

    public class TestQueryWithObjectType
    {
        [UseGrouping]
        public IQueryable<TestEntity> GetTestEntities() =>
            new[] { new TestEntity { Id = 1, Name = "Test", Value = 100 } }.AsQueryable();
    }
}
