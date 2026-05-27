using System.Linq.Expressions;
using HotChocolate.Data;
using HotChocolate.Data.Filters;
using HotChocolate.Data.Filters.Expressions;
using HotChocolate.Data.Grouping.Execution.QueryableGrouping;
using HotChocolate.Execution;
using HotChocolate.Language;
using HotChocolate.Types;
using HotChocolate.Types.Descriptors;
using Microsoft.Extensions.DependencyInjection;

namespace HotChocolate.Data.Grouping;

/// <summary>
/// Smoke tests for <see cref="HavingFilterContext"/>'s operand-parameter override. HotChocolate.Data's
/// stock <see cref="QueryableFilterContext"/> keys the visitor's lambda parameter on the filter
/// input's <c>EntityType</c> — which, for <c>*OperationFilterInputType</c> subclasses, is the
/// filter class itself rather than the operand CLR scalar. HAVING needs the parameter typed to
/// the carrier's slot CLR (e.g. <c>int?</c> for Min/Max(int), <c>long?</c> for Sum(int)). These
/// tests build a context against a real HotChocolate.Data filter type, run the visitor over a sample
/// <c>{ eq: ... }</c> clause, and assert the produced lambda's <c>Parameter.Type</c> matches the
/// supplied operand — not the filter input class.
/// </summary>
public class HavingFilterContextTests
{
    [Theory]
    [InlineData(typeof(int))]
    [InlineData(typeof(int?))]
    [InlineData(typeof(long?))]
    [InlineData(typeof(double?))]
    public async Task LambdaParameterMatchesSlotType_NotFilterInputClass(Type slotType)
    {
        // Build an executor that exposes IntOperationFilterInput on a field — forces HotChocolate.Data to
        // materialise the type so we can resolve a real IFilterInputType instance.
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

        // having: { eq: 5 } — a minimal clause that exercises the comparable equals handler.
        var clause = new ObjectValueNode(new ObjectFieldNode("eq", new IntValueNode(5)));
        visitor.Visit(clause, ctx);

        Assert.Empty(ctx.Errors);
        Assert.True(ctx.TryCreateLambda(out var lambda));

        // The lambda parameter must be typed as the operand slot, NOT as IntOperationFilterInputType.
        Assert.Single(lambda.Parameters);
        Assert.Equal(slotType, lambda.Parameters[0].Type);
        Assert.NotEqual(typeof(IntOperationFilterInputType), lambda.Parameters[0].Type);
    }
}
