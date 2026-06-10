using System.Linq.Expressions;
using HotChocolate.Data.Filters;
using HotChocolate.Data.Filters.Expressions;
using HotChocolate.Data.Grouping.Execution.QueryableGrouping;
using HotChocolate.Execution;
using HotChocolate.Language;
using HotChocolate.Types;
using HotChocolate.Types.Descriptors;
using Microsoft.Extensions.DependencyInjection;

namespace HotChocolate.Data.Grouping;

public class HavingFilterContextTests
{
    [Theory]
    [InlineData(typeof(int))]
    [InlineData(typeof(int?))]
    [InlineData(typeof(long?))]
    [InlineData(typeof(double?))]
    public async Task LambdaParameterMatchesSlotType_NotFilterInputClass(Type slotType)
    {
        await using var provider = new ServiceCollection()
            .AddGraphQL()
            .AddFiltering()
            .AddQueryType(d => d.Name("Q")
                .Field("probe")
                .Argument("having", a => a.Type<IntOperationFilterInputType>())
                .Resolve(1))
            .Services
            .BuildServiceProvider();
        var executor = await provider.GetRequestExecutorAsync();

        var filterType = executor.Schema.Types.GetType<IFilterInputType>("IntOperationFilterInput");
        var operandRuntime = new DefaultTypeInspector().GetType(slotType);

        var ctx = new HavingFilterContext(filterType, operandRuntime, inMemory: false);
        var visitor = new FilterVisitor<QueryableFilterContext, Expression>(new QueryableCombinator());

        var clause = new ObjectValueNode(new ObjectFieldNode("eq", new IntValueNode(5)));
        visitor.Visit(clause, ctx);

        Assert.Empty(ctx.Errors);
        Assert.True(ctx.TryCreateLambda(out var lambda));
        Assert.Single(lambda.Parameters);
        Assert.Equal(slotType, lambda.Parameters[0].Type);
        Assert.NotEqual(typeof(IntOperationFilterInputType), lambda.Parameters[0].Type);
    }
}
