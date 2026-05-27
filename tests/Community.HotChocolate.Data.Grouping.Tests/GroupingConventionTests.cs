#pragma warning disable SA1201 // Elements should appear in the correct order

using System.Text.Json;
using System.Text.Json.Nodes;
using HotChocolate;
using HotChocolate.Data;
using HotChocolate.Data.Filters;
using HotChocolate.Data.Filters.Expressions;
using HotChocolate.Data.Grouping.Aggregates;
using HotChocolate.Data.Grouping.Convention;
using HotChocolate.Data.Grouping.Fields;
using HotChocolate.Execution;
using HotChocolate.Language;
using HotChocolate.Types;
using HotChocolate.Utilities;
using Microsoft.Extensions.DependencyInjection;

namespace HotChocolate.Data.Grouping;

public class GroupingConventionTests
{
    [Fact]
    public async Task SchemaTest()
    {
        var services = new ServiceCollection();

        services
            .AddGraphQL()
            .AddFiltering()
            .AddSorting()
            .AddGrouping<MoneyGroupingConvention>()
            .AddQueryType<MoneyQuery>()
            .AddType<MoneyType>()
            .BindRuntimeType<Money, MoneyType>();

        await using var provider = services.BuildServiceProvider();
        var executor = await provider.GetRequestExecutorAsync();

        await Verify(executor.Schema.ToString())
            .UseDirectory("__snapshots__");

        var schema = executor.Schema;

        // Money registered as a comparable leaf — flat key field, no recursive wrapper.
        Assert.True(schema.Types.ContainsName("ProductGroupingKey"));
        Assert.False(schema.Types.ContainsName("MoneyGroupingKey"));

        // No *AggregateResult mapping for the custom leaf, so it's omitted from the aggregate.
        var aggregate = schema.Types.GetType<IObjectTypeDefinition>("ProductAggregate");
        Assert.DoesNotContain(aggregate.Fields, f => f.Name == "price");

        var key = schema.Types.GetType<IObjectTypeDefinition>("ProductGroupingKey");
        var priceField = Assert.Single(key.Fields, f => f.Name == "price");
        Assert.Equal("Money", priceField.Type.NamedType().Name);
    }

    [Fact]
    public async Task DefaultFilterNullParent_FlowsFromConvention()
    {
        var services = new ServiceCollection();

        services
            .AddGraphQL()
            .AddGrouping(d => d.AddDefaults().DefaultFilterNullParent(true))
            .AddQueryType<MoneyQuery>()
            .AddType<MoneyType>()
            .BindRuntimeType<Money, MoneyType>();

        await using var provider = services.BuildServiceProvider();
        var executor = await provider.GetRequestExecutorAsync();
        var sdl = executor.Schema.ToString();

        Assert.Contains(
            "filterNullParent: Boolean = true",
            sdl,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task DefaultFilterNullParent_DefaultsToFalse()
    {
        var services = new ServiceCollection();

        services
            .AddGraphQL()
            .AddGrouping()
            .AddQueryType<MoneyQuery>()
            .AddType<MoneyType>()
            .BindRuntimeType<Money, MoneyType>();

        await using var provider = services.BuildServiceProvider();
        var executor = await provider.GetRequestExecutorAsync();
        var sdl = executor.Schema.ToString();

        Assert.Contains(
            "filterNullParent: Boolean = false",
            sdl,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task SchemaWithoutFiltering_OmitsHavingArguments()
    {
        // Grouping doesn't force AddFiltering. Without it, no `having:` arg surfaces anywhere.
        var services = new ServiceCollection();
        services
            .AddGraphQL()
            .AddGrouping()
            .AddQueryType<MoneyQuery>()
            .AddType<MoneyType>()
            .BindRuntimeType<Money, MoneyType>();

        await using var provider = services.BuildServiceProvider();
        var executor = await provider.GetRequestExecutorAsync();

        await Verify(executor.Schema.ToString()).UseDirectory("__snapshots__");

        var sdl = executor.Schema.ToString();
        Assert.DoesNotContain("having:", sdl, StringComparison.Ordinal);
        Assert.DoesNotContain("OperationFilterInput", sdl, StringComparison.Ordinal);
    }

    public class MoneyQuery
    {
        [UseGrouping]
        public IQueryable<Product> GetProductGrouping() => SeedProducts.AsQueryable();

        public static readonly Product[] SeedProducts =
        [
            new Product { Id = 1, Name = "Widget", Price = new Money(10m, "USD") },
            new Product { Id = 2, Name = "Gadget", Price = new Money(25m, "USD") },
            new Product { Id = 3, Name = "Gizmo",  Price = new Money(5m,  "USD") },
        ];
    }

    public record Product
    {
        public int Id { get; set; }

        public string Name { get; set; } = default!;

        public Money Price { get; set; }
    }

    public readonly record struct Money(decimal Amount, string Currency) : IComparable<Money>, IComparable
    {
        public override string ToString() => $"{Amount} {Currency}";

        public static Money Parse(string value)
        {
            var parts = value.Split(' ', 2);
            return new Money(decimal.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture), parts[1]);
        }

        public int CompareTo(Money other)
        {
            var c = string.CompareOrdinal(Currency, other.Currency);
            return c != 0 ? c : Amount.CompareTo(other.Amount);
        }

        public int CompareTo(object? obj) => obj is Money m ? CompareTo(m) : 1;
    }

    public class MoneyType : ScalarType<Money, StringValueNode>
    {
        public MoneyType()
            : base("Money")
        {
        }

        protected override Money OnCoerceInputLiteral(StringValueNode valueLiteral) =>
            Money.Parse(valueLiteral.Value);

        protected override Money OnCoerceInputValue(JsonElement inputValue, Features.IFeatureProvider context)
        {
            var raw = inputValue.GetString() ?? throw new InvalidOperationException("Money requires a string.");
            return Money.Parse(raw);
        }

        protected override void OnCoerceOutputValue(Money runtimeValue, Text.Json.ResultElement resultValue)
        {
            resultValue.SetStringValue(runtimeValue.ToString());
        }

        protected override StringValueNode OnValueToLiteral(Money runtimeValue) =>
            new(runtimeValue.ToString());
    }

    public class MoneyGroupingConvention : GroupingConvention
    {
        protected override void Configure(IGroupingConventionDescriptor descriptor)
        {
            descriptor.AddDefaults();
            descriptor.BindComparable<Money>();
        }
    }

    [Fact]
    public async Task BindAggregateTypes_CustomScalar_SurfacesOnAggregate()
    {
        var services = new ServiceCollection();
        services
            .AddGraphQL()
            // Money needs a filter binding so the comparable handlers attach when HAVING uses it.
            .AddFiltering(f => f.AddDefaults().BindRuntimeType<Money, MoneyOperationFilterInputType>())
            .AddGrouping<CustomMoneyConvention>()
            .AddQueryType<MoneyQuery>()
            .AddType<MoneyType>()
            .AddType<MoneyAggregateResultType>()
            .AddType<MoneyOperationFilterInputType>()
            .BindRuntimeType<Money, MoneyType>();

        await using var provider = services.BuildServiceProvider();
        var executor = await provider.GetRequestExecutorAsync();
        var schema = executor.Schema;

        Assert.True(schema.Types.ContainsName("MoneyAggregateResult"));
        Assert.True(schema.Types.ContainsName("MoneyOperationFilterInput"));

        var aggregate = schema.Types.GetType<IObjectTypeDefinition>("ProductAggregate");
        var price = Assert.Single(aggregate.Fields, f => f.Name == "price");
        Assert.Equal("MoneyAggregateResult", price.Type.NamedType().Name);

        // Only the operators the user opted in to — custom filter wins over the default.
        var filter = schema.Types.GetType<IInputObjectTypeDefinition>("MoneyOperationFilterInput");
        Assert.Contains(filter.Fields, f => f.Name == "eq");
        Assert.Contains(filter.Fields, f => f.Name == "neq");
        Assert.DoesNotContain(filter.Fields, f => f.Name == "gt");
    }

    public class CustomMoneyConvention : GroupingConvention
    {
        protected override void Configure(IGroupingConventionDescriptor descriptor)
        {
            descriptor.AddDefaults();
            descriptor.BindRuntimeType<Money, MoneyAggregateResultType>();
        }
    }

    public sealed class MoneyAggregateResultType : AggregateResultType
    {
        protected override void Configure(IAggregateResultTypeDescriptor descriptor)
        {
            descriptor.Name("MoneyAggregateResult");
            descriptor.Operation(AggregationKind.Min).Type(typeof(Money?)).Having<Money>();
            descriptor.Operation(AggregationKind.Max).Type(typeof(Money?)).Having<Money>();
        }
    }

    // Implements IComparableOperationFilterInputType so HotChocolate.Data's stock comparable
    // handlers (eq / neq) attach to its operator fields.
    public sealed class MoneyOperationFilterInputType : FilterInputType, IComparableOperationFilterInputType
    {
        protected override void Configure(IFilterInputTypeDescriptor descriptor)
        {
            descriptor.Name("MoneyOperationFilterInput");
            descriptor.Operation(DefaultFilterOperations.Equals).Type<MoneyType>();
            descriptor.Operation(DefaultFilterOperations.NotEquals).Type<MoneyType>();
        }
    }

    [Fact]
    public async Task BindAggregateTypes_CustomScalar_ResolvesAtRuntime()
    {
        // Three seeded products → one bucket → min "5 USD", max "25 USD".
        var services = new ServiceCollection();
        services
            .AddGraphQL()
            .AddFiltering(f => f.AddDefaults().BindRuntimeType<Money, MoneyOperationFilterInputType>())
            .AddGrouping<CustomMoneyConvention>()
            .AddQueryType<MoneyQuery>()
            .AddType<MoneyType>()
            .AddType<MoneyAggregateResultType>()
            .AddType<MoneyOperationFilterInputType>()
            .BindRuntimeType<Money, MoneyType>();

        await using var provider = services.BuildServiceProvider();
        var executor = await provider.GetRequestExecutorAsync();

        var result = (OperationResult)await executor.ExecuteAsync(
            "{ productGrouping { count aggregate { price { min max } } } }");

        if (result.Errors is { Count: > 0 } errors)
        {
            throw new Exception(string.Join(" | ", errors.Select(e => e.Message)));
        }
        var bucket = Assert.Single((JsonArray)JsonNode.Parse(result.ToJson())!["data"]!["productGrouping"]!);
        Assert.Equal(3, bucket!["count"]!.GetValue<int>());
        Assert.Equal("5 USD", bucket["aggregate"]!["price"]!["min"]!.GetValue<string>());
        Assert.Equal("25 USD", bucket["aggregate"]!["price"]!["max"]!.GetValue<string>());
    }

    [Fact]
    public async Task Filtering_Bindings_AreVisibleToHaving()
    {
        // HAVING reads filter inputs straight from HotChocolate.Data's convention, so any
        // AddFiltering customization (renames, operators, type swaps) flows through automatically.
        var services = new ServiceCollection();
        services
            .AddGraphQL()
            .AddFiltering()
            .AddGrouping()
            .AddQueryType<MoneyQuery>()
            .AddType<MoneyType>()
            .BindRuntimeType<Money, MoneyType>();

        await using var provider = services.BuildServiceProvider();
        var executor = await provider.GetRequestExecutorAsync();

        var stringFilter = executor.Schema.Types.GetType<IInputObjectTypeDefinition>("StringOperationFilterInput");
        Assert.Contains(stringFilter.Fields, f => f.Name == "eq");
        Assert.Contains(stringFilter.Fields, f => f.Name == "contains");
        Assert.Contains(stringFilter.Fields, f => f.Name == "in");

        // HotChocolate.Data ships the comparable family with and/or disabled by default —
        // a consumer can opt in by subclassing + BindRuntimeType (see BindRuntimeType_OrEnabledIntFilter_FlowsToHaving).
        var intFilter = executor.Schema.Types.GetType<IInputObjectTypeDefinition>("IntOperationFilterInput");
        Assert.DoesNotContain(intFilter.Fields, f => f.Name == "and");
        Assert.DoesNotContain(intFilter.Fields, f => f.Name == "or");
    }

    public sealed class OrEnabledIntFilterInputType : IntOperationFilterInputType
    {
        protected override void Configure(IFilterInputTypeDescriptor descriptor)
        {
            base.Configure(descriptor);
            descriptor.Name("OrEnabledIntFilterInput");
            descriptor.AllowAnd();
            descriptor.AllowOr();
        }
    }

    [Fact]
    public async Task BindRuntimeType_OrEnabledIntFilter_FlowsToHaving()
    {
        var services = new ServiceCollection();
        services
            .AddGraphQL()
            .AddFiltering(f => f.AddDefaults().BindRuntimeType<int, OrEnabledIntFilterInputType>())
            .AddGrouping()
            .AddQueryType<MoneyQuery>()
            .AddType<MoneyType>()
            .BindRuntimeType<Money, MoneyType>();

        await using var provider = services.BuildServiceProvider();
        var executor = await provider.GetRequestExecutorAsync();
        var schema = executor.Schema;

        var orFilter = schema.Types.GetType<IInputObjectTypeDefinition>("OrEnabledIntFilterInput");
        Assert.Contains(orFilter.Fields, f => f.Name == "or");
        Assert.Contains(orFilter.Fields, f => f.Name == "and");

        // count(having:) resolves through the convention to the consumer-bound filter input.
        var grouping = schema.Types.GetType<IObjectTypeDefinition>("ProductGrouping");
        var count = Assert.Single(grouping.Fields, f => f.Name == "count");
        var having = Assert.Single(count.Arguments, a => a.Name == "having");
        Assert.Equal("OrEnabledIntFilterInput", having.Type.NamedType().Name);

        // Seed produces one bucket with count=3 — OR with a matching arm keeps it, OR with
        // no matching arm drops it. Proves AllowOr flows to the queryable, not just the schema.
        async Task<int> CountBucketsForHaving(string having)
        {
            var result = (OperationResult)await executor.ExecuteAsync(
                $"{{ productGrouping {{ count(having: {having}) }} }}");
            if (result.Errors is { Count: > 0 } errors)
            {
                throw new Exception(string.Join(" | ", errors.Select(e => e.Message)));
            }
            return ((JsonArray)JsonNode.Parse(result.ToJson())!["data"]!["productGrouping"]!).Count;
        }

        Assert.Equal(1, await CountBucketsForHaving("{ or: [{ eq: 3 }, { eq: 99 }] }"));
        Assert.Equal(0, await CountBucketsForHaving("{ or: [{ eq: 1 }, { eq: 99 }] }"));
    }

    [Fact]
    public async Task Operation_Rename_ReshapesSchemaAndDispatch()
    {
        // Renaming `eq` to `equals` on the filter convention reshapes both the schema HAVING
        // input and runtime dispatch — no grouping-side operation table.
        var services = new ServiceCollection();
        services
            .AddGraphQL()
            .AddFiltering(f =>
            {
                f.AddDefaults();
                f.Operation(DefaultFilterOperations.Equals).Name("equals");
            })
            .AddGrouping()
            .AddQueryType<UInt64Query>();

        await using var provider = services.BuildServiceProvider();
        var executor = await provider.GetRequestExecutorAsync();

        // Schema: IntOperationFilterInput now exposes `equals`, not `eq`.
        var filter = executor.Schema.Types.GetType<IInputObjectTypeDefinition>("IntOperationFilterInput");
        Assert.Contains(filter.Fields, f => f.Name == "equals");
        Assert.DoesNotContain(filter.Fields, f => f.Name == "eq");

        // Runtime: a query using the renamed operator dispatches correctly.
        var result = (OperationResult)await executor.ExecuteAsync(
            "{ recordGrouping { count(having: { equals: 2 }) } }");
        if (result.Errors is { Count: > 0 } errors)
        {
            throw new Exception(string.Join(" | ", errors.Select(e => $"{e.Message}: {e.Exception?.Message}")));
        }
        var bucket = Assert.Single((JsonArray)JsonNode.Parse(result.ToJson())!["data"]!["recordGrouping"]!);
        Assert.Equal(2, bucket!["count"]!.GetValue<int>());
    }

    public record UInt64Record(int Id, ulong Value);

    public class UInt64Query
    {
        [UseGrouping]
        public IQueryable<UInt64Record> GetRecordGrouping() => new[]
        {
            new UInt64Record(1, 100UL),
            new UInt64Record(2, 200UL),
        }.AsQueryable();
    }

    [Fact]
    public async Task CustomOperator_DivisibleBy_RegistersAndDispatches()
    {
        // Three coordinated declarations register `divisibleBy: Int` on IntOperationFilterInput:
        // operation id+name, field overlay via Configure<TFilter>, and a custom handler.
        // Seed → one bucket with count=2 → `divisibleBy: 2` matches, `divisibleBy: 3` doesn't.
        var services = new ServiceCollection();
        services
            .AddGraphQL()
            .AddFiltering(f =>
            {
                f.AddDefaults();
                f.Operation(CustomOps.DivisibleBy).Name("divisibleBy");
                f.Configure<IntOperationFilterInputType>(d =>
                    d.Operation(CustomOps.DivisibleBy).Type<IntType>());
                f.Provider(new HotChocolate.Data.Filters.Expressions.QueryableFilterProvider(p =>
                    p.AddDefaultFieldHandlers().AddFieldHandler(DivisibleByHandler.Create)));
            })
            .AddGrouping()
            .AddQueryType<UInt64Query>();

        await using var provider = services.BuildServiceProvider();
        var executor = await provider.GetRequestExecutorAsync();

        var filter = executor.Schema.Types.GetType<IInputObjectTypeDefinition>("IntOperationFilterInput");
        Assert.Contains(filter.Fields, f => f.Name == "divisibleBy");

        var matched = (OperationResult)await executor.ExecuteAsync(
            "{ recordGrouping { count(having: { divisibleBy: 2 }) } }");
        if (matched.Errors is { Count: > 0 } me)
        {
            throw new Exception(string.Join(" | ", me.Select(e => $"{e.Message}: {e.Exception?.Message}")));
        }
        var matchedBuckets = (JsonArray)JsonNode.Parse(matched.ToJson())!["data"]!["recordGrouping"]!;
        Assert.Equal(2, Assert.Single(matchedBuckets)!["count"]!.GetValue<int>());

        var unmatched = (OperationResult)await executor.ExecuteAsync(
            "{ recordGrouping { count(having: { divisibleBy: 3 }) } }");
        var unmatchedBuckets = (JsonArray)JsonNode.Parse(unmatched.ToJson())!["data"]!["recordGrouping"]!;
        Assert.Empty(unmatchedBuckets);
    }

    private static class CustomOps
    {
        // Above HotChocolate.Data's reserved 0-29 range for built-in operations.
        public const int DivisibleBy = 100;
    }

    public sealed class DivisibleByHandler : QueryableComparableOperationHandler
    {
        public DivisibleByHandler(ITypeConverter typeConverter, InputParser inputParser)
            : base(typeConverter, inputParser)
        {
        }

        protected override int Operation => CustomOps.DivisibleBy;

        public override System.Linq.Expressions.Expression HandleOperation(
            HotChocolate.Data.Filters.Expressions.QueryableFilterContext context,
            IFilterOperationField field,
            IValueNode value,
            object? parsedValue)
        {
            var property = context.GetInstance();
            parsedValue = ParseValue(value, parsedValue, field.Type, context);
            var operand = System.Linq.Expressions.Expression.Constant(parsedValue, property.Type);
            var zero = System.Linq.Expressions.Expression.Constant(
                Activator.CreateInstance(Nullable.GetUnderlyingType(property.Type) ?? property.Type)!,
                property.Type);
            return System.Linq.Expressions.Expression.Equal(
                System.Linq.Expressions.Expression.Modulo(property, operand),
                zero);
        }

        public static DivisibleByHandler Create(HotChocolate.Data.Filters.FilterProviderContext context)
            => new(context.TypeConverter, context.InputParser);
    }
}
