---
title: Convention
---

# Convention

Controls which CLR types are *leaves* (eligible to appear in `key` / aggregates), how they're aggregated (numeric vs. comparable), and the default `filterNullParent` value.

## Configuration

```csharp
builder.Services
    .AddGraphQL()
    .AddGrouping(d => d
        .BindNumeric<Money>()             // key + avg/sum/min/max
        .BindComparable<Email>()          // key + min/max only
        .DefaultFilterNullParent(true))
    .AddQueryType<Query>();
```

- `BindNumeric<T>()` — `T` can appear in `key`, `min`, `max`, `avg`, `sum`.
- `BindComparable<T>()` — `T` can appear in `key`, `min`, `max` only.

Every primitive, `string`, `Guid`, `DateTime`/`DateTimeOffset`/`DateOnly`/`TimeOnly`/`TimeSpan`, and enums are bound out of the box — see `GroupingConventionDescriptor` for the full list.

## Restricting allowed aggregations

By default every bound type exposes all the operations its kind supports (numeric → `avg`/`sum`/`min`/`max`, comparable → `min`/`max`). To narrow the surface schema-wide, pass a `GroupingAggregations` mask:

```csharp
.AddGrouping(d => d.AllowedAggregations(GroupingAggregations.Min | GroupingAggregations.Max))
```

- Operations outside the mask never appear on any `*AggregateResult` type.
- Filtering is **schema-level**: a Comparable scalar disappears from `aggregate` if you exclude both `min` and `max`, because its result type would otherwise have no fields.

To drop aggregates entirely so groupings expose only `key` and `count`:

```csharp
.AddGrouping(d => d.AllowedAggregations(GroupingAggregations.None))
```

## Custom aggregate result types

For a scalar that needs an `*AggregateResult` shape the built-ins don't cover (different operations or a HAVING input tailored to the type), bind the result type:

```csharp
descriptor.BindRuntimeType<Money, MoneyAggregateResultType>();
```

The result type derives from `AggregateResultType`. Each operation wires its HAVING input by **runtime type**, not filter type — `.Having<Money>()` tells the grouping side "use whatever filter input HotChocolate.Data has bound for `Money`". Customizations on the filter side (`AddFiltering(f => f.BindRuntimeType<Money, MyMoneyFilter>())`) flow through automatically. See the test project's `MoneyAggregateResultType` for a template, and [HAVING Filtering → Custom-scalar HAVING](./having-filtering#custom-scalar-having) for the matching filter-side binding.

## Example: custom scalar leaf

For a value-object like `Money` that should appear as a single scalar (not a composite), register the HotChocolate scalar and the grouping binding together:

```csharp
public record Money(decimal Amount, string Currency);

services
    .AddGraphQL()
    .AddGrouping(d => d.BindComparable<Money>())
    .AddType<MoneyType>()
    .BindRuntimeType<Money, MoneyType>()
    .AddQueryType<Query>();
```

```graphql
query {
  productGrouping {
    key { price }
    aggregate { price { min max } }
  }
}
```

`Money` appears flat in `key` (not as a nested `MoneyGroupingKey`). It exposes `min`/`max` only because it's bound as comparable — use `BindNumeric<Money>()` to add `avg`/`sum`.

:::note[EF Core mapping]
EF Core needs to see `Money` as a single primitive column for `GROUP BY` / `MIN` / `MAX` to translate. A value converter is the simplest path:

```csharp
modelBuilder.Entity<Product>()
    .Property(p => p.Price)
    .HasConversion(v => v.ToString(), v => Money.Parse(v));
```
:::
