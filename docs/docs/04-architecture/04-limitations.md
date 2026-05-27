---
title: Limitations
---

# Limitations

## Max 16-field selection per group or aggregate

Each grouping query builds carrier types — one for the `GroupBy` key, one for the projected aggregates, one for the outer row. Each is capped at 16 fields; queries needing more throw `NotSupportedException` (surfaced as `GROUPING_NOT_SUPPORTED`).

## No server-side ordering

`UseGrouping` materialises the result into an in-memory bucket list — the response is no longer an `IQueryable`, so anything post-`GROUP BY` can't be appended to the pipeline.

### `[UseSorting]` is not compatible

```csharp
// ❌ unsupported
[UseGrouping]
[UseSorting]
public IQueryable<Employee> GetEmployeeGrouping(MyDbContext db) => db.Employees;
```

The schema does not expose an `order` argument on grouped fields.

**Workarounds**: sort client-side (bucket lists are typically small), pre-filter with `where`/`having`, include enough `key` columns to sort lexicographically, or write a custom resolver.


## OR across different aggregates

`having` is per-operation. Multiple aggregate clauses AND-combine; there's no syntax to OR-combine:

```graphql
# ✅ implicit AND across aggregates
{
  employeeGrouping {
    count(having: { gt: 5 })
    aggregate { salary { sum(having: { gt: 100000 }) } }
  }
}

# ❌ not expressible — OR across different aggregates
# "buckets with count > 5 OR salary.sum > 100k"
```

`or:` **within** a single `having: { … }` clause is supported on filter inputs that expose it — see [HAVING Filtering → Enabling OR within a HAVING clause](../configuration/having-filtering#enabling-or-within-a-having-clause).

**Workarounds**: add a key dimension that distinguishes the cases so buckets separate naturally, or split into two requests and union client-side.

## Aggregating over a collection

An aggregate path crossing a collection navigation (`projects { budget { sum } }`) is **only valid when the same collection appears in `key`**. Rejected at parse time with `GROUPING_AGGREGATE_COLLECTION_MISSING_FROM_KEY`:

```graphql
# ❌ rejected
query {
  employeeGrouping {
    key { department { name } }
    aggregate { projects { budget { sum } } }
  }
}
```

Selecting a collection inside `aggregate` would expand the source into one row per element — silently changing which buckets the query returns and what `count` means. Requiring the collection in `key` makes the expansion visible at the call site.

Either drop the collection aggregate:

```graphql
employeeGrouping {
  key { department { name } }
  count
  aggregate { salary { avg } }
}
```

…or add the collection to `key`:

```graphql
employeeGrouping {
  key { department { name }, projects { name } }
  count
  aggregate { projects { budget { sum } } }
}
```

Each parent contributes one row per matching element; `count` is the flattened row count. See [Aggregations → Aggregating across a collection key](../examples/aggregations#aggregating-across-a-collection-key).
