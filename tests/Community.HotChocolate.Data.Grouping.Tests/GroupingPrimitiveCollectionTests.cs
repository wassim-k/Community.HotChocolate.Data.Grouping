using System.Text.Json.Nodes;
using HotChocolate.Data.Grouping.Convention;
using HotChocolate.Execution;
using Microsoft.Extensions.DependencyInjection;

namespace HotChocolate.Data.Grouping;

public class GroupingPrimitiveCollectionTests
{
    [Fact]
    public async Task PrimitiveCollectionKeyAndAggregate_FlattensElements()
    {
        await using var provider = new ServiceCollection()
            .AddGraphQL()
            .AddFiltering()
            .AddGrouping(d => d.AddDefaults())
            .AddQueryType<Query>()
            .Services
            .BuildServiceProvider();
        var executor = await provider.GetRequestExecutorAsync();

        var result = await executor.ExecuteAsync(
            OperationRequestBuilder.New()
                .SetDocument(
                    """
                    query {
                        records {
                            key { scores }
                            count
                            aggregate { scores { avg sum min max } }
                        }
                    }
                    """)
                .Build());

        var json = JsonNode.Parse(result.ToJson())!;
        Assert.Null(json["errors"]);

        var rows = json["data"]!["records"]!.AsArray()
            .Select(row => new
            {
                Score = row!["key"]!["scores"]!.GetValue<int>(),
                Count = row["count"]!.GetValue<int>(),
                Avg = row["aggregate"]!["scores"]!["avg"]!.GetValue<double>(),
                Sum = row["aggregate"]!["scores"]!["sum"]!.GetValue<long>(),
                Min = row["aggregate"]!["scores"]!["min"]!.GetValue<int>(),
                Max = row["aggregate"]!["scores"]!["max"]!.GetValue<int>(),
            })
            .OrderBy(row => row.Score)
            .ToArray();

        Assert.Equal(
            [
                new { Score = 1, Count = 1, Avg = 1d, Sum = 1L, Min = 1, Max = 1 },
                new { Score = 2, Count = 2, Avg = 2d, Sum = 4L, Min = 2, Max = 2 },
                new { Score = 3, Count = 1, Avg = 3d, Sum = 3L, Min = 3, Max = 3 },
            ],
            rows);
    }

    [Fact]
    public async Task PrimitiveCollectionAggregateWithoutKey_IsRejected()
    {
        await using var provider = new ServiceCollection()
            .AddGraphQL()
            .AddFiltering()
            .AddGrouping(d => d.AddDefaults())
            .AddQueryType<Query>()
            .Services
            .BuildServiceProvider();
        var executor = await provider.GetRequestExecutorAsync();

        var result = await executor.ExecuteAsync(
            OperationRequestBuilder.New()
                .SetDocument(
                    """
                    query {
                        records {
                            aggregate { scores { sum } }
                        }
                    }
                    """)
                .Build());

        var operation = Assert.IsType<OperationResult>(result);
        var error = Assert.Single(operation.Errors!);
        Assert.Equal(GroupingErrorCodes.AggregateCollectionMissingFromKey, error.Code);
    }

    [Fact]
    public async Task PrimitiveCollectionBehindNullableParent_WithFilterNullParent_DoesNotRebaseBeforePrefix()
    {
        await using var provider = new ServiceCollection()
            .AddGraphQL()
            .AddFiltering()
            .AddGrouping(d => d.AddDefaults())
            .AddQueryType<Query>()
            .Services
            .BuildServiceProvider();
        var executor = await provider.GetRequestExecutorAsync();

        var result = await executor.ExecuteAsync(
            OperationRequestBuilder.New()
                .SetDocument(
                    """
                    query {
                        records(filterNullParent: true) {
                            key { container { scores } }
                            count
                        }
                    }
                    """)
                .Build());

        var json = JsonNode.Parse(result.ToJson())!;
        Assert.Null(json["errors"]);

        var rows = json["data"]!["records"]!.AsArray()
            .Select(row => new
            {
                Score = row!["key"]!["container"]!["scores"]!.GetValue<int>(),
                Count = row["count"]!.GetValue<int>(),
            })
            .OrderBy(row => row.Score)
            .ToArray();

        Assert.Equal(
            [
                new { Score = 4, Count = 1 },
                new { Score = 5, Count = 1 },
            ],
            rows);
    }

    public class Query
    {
        [UseGrouping]
        public IQueryable<Record> GetRecords() =>
            new[]
            {
                new Record { Scores = [1, 2] },
                new Record { Scores = [2, 3] },
                new Record { Scores = [] },
                new Record { Scores = null },
                new Record { Container = new ScoreContainer { Scores = [4, 5] } },
                new Record { Container = new ScoreContainer { Scores = [] } },
                new Record { Container = null },
            }.AsQueryable();
    }

    public sealed class Record
    {
        public int[]? Scores { get; set; }

        public ScoreContainer? Container { get; set; }
    }

    public sealed class ScoreContainer
    {
        public int[]? Scores { get; set; }
    }
}
