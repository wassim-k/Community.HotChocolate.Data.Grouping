---
title: HAVING Filtering
---

# HAVING Filtering

`having:` arguments delegate entirely to HotChocolate.Data's filter convention. The grouping side wires HAVING by **runtime type** and HotChocolate.Data resolves the matching `*OperationFilterInputType` — operator renames, custom operators, and per-runtime-type bindings on the filter side flow through to HAVING with no grouping-side glue.

## HAVING needs `AddFiltering`

`AddGrouping(...)` does not force `AddFiltering(...)` on consumers. When `AddFiltering` isn't registered, `having:` arguments are silently omitted and grouping still works for `key` / `aggregate`.

```csharp
services
    .AddGraphQL()
    .AddFiltering()   // required for `having:` to surface
    .AddGrouping()
    .AddQueryType<Query>();
```

## Custom-scalar HAVING

Register the filter input on HotChocolate.Data's side:

```csharp
services
    .AddGraphQL()
    .AddFiltering(f => f
        .AddDefaults()
        .BindRuntimeType<Money, MoneyOperationFilterInputType>())   // filter side
    .AddGrouping(d => d
        .BindRuntimeType<Money, MoneyAggregateResultType>())        // grouping side
    .AddQueryType<Query>();
```

Operator names, custom operators, and per-type overlays go through `AddFiltering` — see the `Operation_Rename` and `CustomOperator_DivisibleBy` tests for the full pipeline.

## Enabling OR within a HAVING clause

HotChocolate.Data ships numeric/comparable filter inputs (`IntOperationFilterInput`, `LongOperationFilterInput`, `BooleanOperationFilterInput`, etc.) with `and`/`or` **disabled** by default. `StringOperationFilterInput` ships with them enabled.

Convention-level `f.AllowOr()` is overridden by the per-type Configure, and `f.Configure<TFilter>(d => d.AllowOr())` silently no-ops (the `UseAnd`/`UseOr` scalars get dropped by `DataTypeExtensionHelper`). The reliable path is a `TypeInterceptor` that flips the flags as each filter input completes:

```csharp
using HotChocolate.Configuration;
using HotChocolate.Data.Filters;
using HotChocolate.Types.Descriptors.Configurations;

public sealed class EnableAndOrOnFilterInputs : TypeInterceptor
{
    public override void OnBeforeCompleteType(
        ITypeCompletionContext completionContext,
        TypeSystemConfiguration configuration)
    {
        if (configuration is FilterInputTypeConfiguration filter)
        {
            filter.UseAnd = true;
            filter.UseOr = true;
        }
    }
}

services
    .AddGraphQL()
    .AddFiltering()
    .AddGrouping()
    .TryAddTypeInterceptor<EnableAndOrOnFilterInputs>()
    .AddQueryType<Query>();
```

This affects regular `[UseFiltering]` surfaces too — usually what you want.

```graphql
{
  employeeGrouping {
    count(having: { or: [{ eq: 3 }, { gt: 10 }] })
  }
}
```

OR composition **across different aggregate operations** is a separate limitation — see [Limitations → OR across different aggregates](../architecture/limitations#or-across-different-aggregates).
