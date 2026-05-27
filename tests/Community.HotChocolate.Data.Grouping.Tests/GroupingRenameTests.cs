#pragma warning disable SA1201 // Elements should appear in the correct order

using HotChocolate;
using HotChocolate.Data;
using HotChocolate.Execution;
using HotChocolate.Types;
using Microsoft.Extensions.DependencyInjection;

namespace HotChocolate.Data.Grouping;

public class GroupingRenameTests
{
    [Fact]
    public async Task SchemaTest()
    {
        var services = new ServiceCollection();

        services
            .AddGraphQL()
            .AddFiltering()
            .AddSorting()
            .AddGrouping()
            .AddQueryType<RenamedTestQuery>()
            .AddType<CustomNamedThingType>()
            .AddType<CustomNamedNestedType>();

        await using var provider = services.BuildServiceProvider();
        var executor = await provider.GetRequestExecutorAsync();

        await Verify(executor.Schema.ToString())
            .UseDirectory("__snapshots__");

        var schema = executor.Schema;

        Assert.True(schema.Types.ContainsName("CustomNamedThingGrouping"));
        Assert.True(schema.Types.ContainsName("CustomNamedThingGroupingKey"));
        Assert.True(schema.Types.ContainsName("CustomNamedThingAggregate"));
        Assert.True(schema.Types.ContainsName("CustomNamedNestedGroupingKey"));
        Assert.True(schema.Types.ContainsName("CustomNamedNestedAggregate"));
        Assert.True(schema.Types.ContainsName("CustomNamedThing"));
        Assert.False(schema.Types.ContainsName("TestThingGrouping"));

        // Sanity: only one ObjectType claims typeof(TestThing) — no duplicate from our wrappers.
        var thingBindings = schema.Types
            .OfType<IObjectTypeDefinition>()
            .Count(t => t.RuntimeType == typeof(TestThing));
        Assert.Equal(1, thingBindings);
    }

    public class RenamedTestQuery
    {
        [UseGrouping]
        public IQueryable<TestThing> GetTestThingGrouping() => Array.Empty<TestThing>().AsQueryable();
    }

    public record TestThing
    {
        public int Id { get; set; }

        public string Name { get; set; } = default!;

        public decimal Amount { get; set; }

        public TestNested? Nested { get; set; }
    }

    public record TestNested
    {
        public int Id { get; set; }

        public string Label { get; set; } = default!;

        public int Weight { get; set; }
    }

    public class CustomNamedThingType : ObjectType<TestThing>
    {
        protected override void Configure(IObjectTypeDescriptor<TestThing> descriptor)
            => descriptor.Name("CustomNamedThing");
    }

    public class CustomNamedNestedType : ObjectType<TestNested>
    {
        protected override void Configure(IObjectTypeDescriptor<TestNested> descriptor)
            => descriptor.Name("CustomNamedNested");
    }

    [Fact]
    public async Task SchemaTest_WithRenamedField()
    {
        var services = new ServiceCollection();

        services
            .AddGraphQL()
            .AddFiltering()
            .AddSorting()
            .AddGrouping()
            .AddQueryType<WidgetQuery>()
            .AddType<WidgetType>();

        await using var provider = services.BuildServiceProvider();
        var executor = await provider.GetRequestExecutorAsync();

        await Verify(executor.Schema.ToString())
            .UseDirectory("__snapshots__");

        var schema = executor.Schema;

        // The consumer type keeps its default name (the rename is only at field level).
        Assert.True(schema.Types.ContainsName("Widget"));
        Assert.True(schema.Types.ContainsName("WidgetGroupingKey"));
        Assert.True(schema.Types.ContainsName("WidgetAggregate"));

        var groupingKey = (IObjectTypeDefinition)schema.Types["WidgetGroupingKey"];
        Assert.True(groupingKey.Fields.ContainsName("displayName"));
        Assert.False(groupingKey.Fields.ContainsName("label"));
        Assert.True(groupingKey.Fields.ContainsName("id"));
        Assert.True(groupingKey.Fields.ContainsName("price"));

        // Renames also propagate to the aggregate type — `displayName` not `label`.
        var aggregate = (IObjectTypeDefinition)schema.Types["WidgetAggregate"];
        Assert.True(aggregate.Fields.ContainsName("displayName"));
        Assert.False(aggregate.Fields.ContainsName("label"));
        Assert.True(aggregate.Fields.ContainsName("price"));
    }

    public class WidgetQuery
    {
        [UseGrouping]
        public IQueryable<Widget> GetWidgetGrouping() => Array.Empty<Widget>().AsQueryable();
    }

    public record Widget
    {
        public int Id { get; set; }

        public string Label { get; set; } = default!;

        public decimal Price { get; set; }
    }

    public class WidgetType : ObjectType<Widget>
    {
        protected override void Configure(IObjectTypeDescriptor<Widget> descriptor)
        {
            descriptor.Field(w => w.Label).Name("displayName");
        }
    }

    [Fact]
    public async Task SchemaTest_WithIgnoredField()
    {
        var services = new ServiceCollection();

        services
            .AddGraphQL()
            .AddFiltering()
            .AddSorting()
            .AddGrouping()
            .AddQueryType<GadgetQuery>()
            .AddType<GadgetType>();

        await using var provider = services.BuildServiceProvider();
        var executor = await provider.GetRequestExecutorAsync();

        await Verify(executor.Schema.ToString())
            .UseDirectory("__snapshots__");

        var schema = executor.Schema;

        var groupingKey = (IObjectTypeDefinition)schema.Types["GadgetGroupingKey"];
        Assert.True(groupingKey.Fields.ContainsName("id"));
        Assert.True(groupingKey.Fields.ContainsName("price"));
        Assert.False(groupingKey.Fields.ContainsName("secret"));

        // Ignore() on the consumer ObjectType propagates to the aggregate too.
        var aggregate = (IObjectTypeDefinition)schema.Types["GadgetAggregate"];
        Assert.True(aggregate.Fields.ContainsName("price"));
        Assert.False(aggregate.Fields.ContainsName("secret"));
    }

    public class GadgetQuery
    {
        [UseGrouping]
        public IQueryable<Gadget> GetGadgetGrouping() => Array.Empty<Gadget>().AsQueryable();
    }

    public record Gadget
    {
        public int Id { get; set; }

        public decimal Price { get; set; }

        public decimal Secret { get; set; }
    }

    public class GadgetType : ObjectType<Gadget>
    {
        protected override void Configure(IObjectTypeDescriptor<Gadget> descriptor)
        {
            descriptor.Ignore(g => g.Secret);
        }
    }

}
