#pragma warning disable SA1201 // Elements should appear in the correct order
#pragma warning disable SA1402

using HotChocolate.Execution;
using Microsoft.Extensions.DependencyInjection;

namespace HotChocolate.Data.Grouping;

public class GroupingErrorTests
{
    [Fact]
    public async Task PropertyGetterThrows_SurfacesAsGraphQLError()
    {
        var services = new ServiceCollection();
        services
            .AddGraphQL()
            .AddGrouping()
            .AddQueryType<ThrowingQuery>();

        await using var provider = services.BuildServiceProvider();
        var executor = await provider.GetRequestExecutorAsync();

        var result = (OperationResult)await executor.ExecuteAsync(
            """{ throwingEntityGrouping { key { id } count aggregate { v { sum } } } }""");

        var error = Assert.Single(result.Errors!);
        Assert.Equal(GroupingErrorCodes.ResolverFailed, error.Code);
        Assert.Contains("boom", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Unexpected Execution Error", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EntityNamedGrouping_ThrowsSchemaException()
    {
        var services = new ServiceCollection();
        services
            .AddGraphQL()
            .AddGrouping()
            .AddQueryType<PathologicallyNamedQuery>();

        await using var provider = services.BuildServiceProvider();

        var ex = await Assert.ThrowsAsync<SchemaException>(
            async () => await provider.GetRequestExecutorAsync());

        Assert.Contains("Grouping", ex.Message, StringComparison.Ordinal);
        Assert.Contains("reserved", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // --- bespoke entity types -------------------------------------------

    public record ThrowingEntity
    {
        public int Id { get; set; }

        public decimal V => throw new InvalidOperationException("boom");
    }

    public class ThrowingQuery
    {
        [UseGrouping]
        public IQueryable<ThrowingEntity> GetThrowingEntityGrouping() =>
            new[] { new ThrowingEntity { Id = 1 } }.AsQueryable();
    }

    public record Grouping
    {
        public int Id { get; set; }

        public string Name { get; set; } = default!;
    }

    public class PathologicallyNamedQuery
    {
        [UseGrouping]
        public IQueryable<Grouping> GetGroupingGrouping() => Array.Empty<Grouping>().AsQueryable();
    }
}
