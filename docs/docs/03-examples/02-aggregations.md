---
title: Aggregations
---

# Aggregations

A `*Grouping` bucket has three peer selections: `key`, `count`, `aggregate`. `count` is the bucket row count. `aggregate` is **field-first** — pick the field (or navigation), then the operations.

```graphql
employeeGrouping {
  key { … }
  count                                    # bucket row count
  aggregate {
    salary { avg sum min max }             # one field, four operations
    name   { min max }                     # min / max accept any comparable leaf
    department { budget { max } }          # navigation → nested aggregate
  }
}
```

Collection-navigation aggregates (e.g. `projects { budget { sum } }`) are allowed only when the same collection appears in `key` — see [Aggregating across a collection key](#aggregating-across-a-collection-key).

## The shape of an aggregate

Every scalar leaf is exposed as an `*AggregateResult`. Numeric scalars expose `avg sum min max`; comparable non-numeric types expose `min max`.

| Source scalar                          | Result type                  | Operations          |
|----------------------------------------|------------------------------|---------------------|
| `byte`, `sbyte`, `short`, `ushort`, `int` | `IntAggregateResult`      | `avg sum min max`   |
| `uint`                                 | `UnsignedIntAggregateResult` | `avg sum min max`   |
| `long`, `ulong`                        | `LongAggregateResult`        | `avg sum min max`   |
| `decimal`                              | `DecimalAggregateResult`     | `avg sum min max`   |
| `float`, `double`                      | `FloatAggregateResult`       | `avg sum min max`   |
| `string`                               | `StringAggregateResult`      | `min max`           |
| `bool`                                 | `BooleanAggregateResult`     | `min max`           |
| `Guid`                                 | `UuidAggregateResult`        | `min max`           |
| `DateTime`/`DateTimeOffset`            | `DateTime*AggregateResult`   | `min max`           |
| `DateOnly`/`TimeOnly`                  | `*AggregateResult`           | `min max`           |
| `TimeSpan`                             | `TimeSpanAggregateResult`    | `min max`           |

Every operation slot is nullable — empty buckets project `null` rather than throwing. Null values are excluded from `avg`/`sum` (the divisor for `avg` is the count of non-null values).

Numeric `avg`/`sum` widen to prevent overflow (`int.sum → Long`, `long.sum → Decimal`, `int.avg → Float`). `min`/`max` keep the source type except where it can't hold the value — `uint.min`/`uint.max` widen to `Long`, `ulong` to `Decimal`.

`count: Int!` is a peer of `aggregate` (not a field of `*Aggregate`), takes `having: IntOperationFilterInput`, and selects identically at every depth.

## count

```graphql
query {
  employeeGrouping {
    key { department { name } }
    count
  }
}
```

The bucket row count, post-flattening when collection navigations are involved.

## avg and sum

```graphql
query {
  employeeGrouping {
    key { company { name } }
    aggregate {
      bonus  { avg }
      salary { sum }
    }
  }
}
```

```json
[
  { "key": { "company": { "name": "Acme" } },   "aggregate": { "bonus": { "avg": 15000 },  "salary": { "sum": 360000 } } },
  { "key": { "company": { "name": "Globex" } }, "aggregate": { "bonus": { "avg": 5166.67 }, "salary": { "sum": 345000 } } }
]
```

Acme has 4 employees but only 2 (Alice, Carol) have non-null bonuses — `bonus.avg = (10000 + 20000) / 2 = 15000`, not `30000 / 4`.

## min and max

```graphql
query {
  employeeGrouping {
    key { company { name } }
    aggregate {
      salary { min max }
      name   { min max }       # any comparable leaf
    }
  }
}
```

`min` / `max` accept numeric, string, date, `Guid`, enum, custom comparable.

## Filtering buckets with `having`

Every operation slot takes a `having` argument — buckets that fail the predicate are dropped after the GROUP BY (the SQL `HAVING` analogue).

```graphql
query {
  employeeGrouping {
    key { company { name } }
    count(having: { gt: 2 })
    aggregate {
      salary { sum(having: { gt: 100000 }) avg }
    }
  }
}
```

The input is HotChocolate.Data's `*OperationFilterInput` for the operation's *result* scalar — `count` takes `IntOperationFilterInput`, `salary.sum` (Long) takes `LongOperationFilterInput`, `salary.avg` (Float) takes `FloatOperationFilterInput`. Predicates from multiple `having` arguments AND-combine: a bucket survives only if every clause holds.

| Operator                                                                           | Available on                                  | Meaning              |
|------------------------------------------------------------------------------------|-----------------------------------------------|----------------------|
| `eq` / `neq`                                                                       | all                                           | equality             |
| `gt` / `gte` / `lt` / `lte`                                                        | ordered scalars (everything except `Boolean`) | comparisons          |
| `ngt` / `ngte` / `nlt` / `nlte`                                                    | ordered scalars                               | negated comparisons  |
| `in` / `nin`                                                                       | all                                           | set membership       |
| `contains` / `ncontains` / `startsWith` / `nstartsWith` / `endsWith` / `nendsWith` | `String`                                      | substring matches    |

Composition is **implicit AND**: `{ gt: 1, lt: 5 }` means `> 1 AND < 5`. Booleans only support `eq`/`neq`.

> **OR within a HAVING clause** ships enabled on `StringOperationFilterInput`, disabled on numeric/comparable. Opt in globally with a `TypeInterceptor` — see [HAVING Filtering → Enabling OR within a HAVING clause](../configuration/having-filtering#enabling-or-within-a-having-clause). OR **across different aggregates** is a separate limitation — see [Limitations → OR across different aggregates](../architecture/limitations#or-across-different-aggregates).

### Null semantics

- **`eq: null` / `neq: null`** emit explicit `IS NULL` / `IS NOT NULL` (not lifted equality, which would diverge between in-memory and EF).
- **`nin: [...]`** rejects null aggregate slots, matching SQL `NOT IN` semantics. A bucket whose `avg(bonus)` is null is never included by a `nin` clause.

## Aggregating across a collection key

A `sum`/`avg`/`min`/`max` leaf path that crosses a collection navigation (e.g. `projects { budget { sum } }`) is **only valid when the same collection appears in `key`**. Rejected at parse time with `GROUPING_AGGREGATE_COLLECTION_MISSING_FROM_KEY`:

```graphql
# ❌ rejected — projects appears in aggregate but not in key
query {
  employeeGrouping {
    key { department { name } }
    aggregate { projects { budget { sum } } }
  }
}
```

Selecting a collection inside `aggregate` would expand the source into one row per element — silently changing which buckets the query returns and what `count` means. Requiring the collection in `key` makes the expansion visible at the call site.

Two ways to fix it. Drop the collection aggregate if you wanted per-entity stats:

```graphql
employeeGrouping {
  key { department { name } }
  count
  aggregate { salary { avg } }
}
```

Or add the collection to `key` to opt into the flatten. The bucket then holds `(parent, element)` pairs and aggregates on the **element side** run cleanly — each element contributes exactly once per pair, no fan-out:

```graphql
query {
  employeeGrouping {
    key { projects { name } }
    count
    aggregate {
      projects { budget { avg sum min max } }
    }
  }
}
```

The `"Alpha"` bucket:

```json
{
  "key": { "projects": { "name": "Alpha" } },
  "count": 4,
  "aggregate": {
    "projects": {
      "budget": { "avg": 11250, "sum": 45000, "min": 8000, "max": 15000 }
    }
  }
}
```

4 (employee, Alpha-project) rows: Alice's Alpha (10k) + Bob's Alpha (15k) + Dave's Alpha (12k) + Grace's Alpha (8k). Budgets sum to 45k, average to 11250, range 8k–15k. SQL `JOIN` semantics — see [Grouping → Collection-navigation keys](./grouping#collection-navigation-keys).

## Aggregating without a key

Omit `key` entirely to collapse the entire source into one bucket:

```graphql
query {
  employeeGrouping {
    count
    aggregate { salary { avg sum min max } }
  }
}
```

```json
[
  {
    "count": 8,
    "aggregate": {
      "salary": { "avg": 88125, "sum": 705000, "min": 60000, "max": 120000 }
    }
  }
]
```

Always a one-element array. The middleware emits `GroupBy(_ => 0)` which providers translate to an ungrouped aggregate query (no `GROUP BY` clause in SQL).

Pair with `[UseFiltering]` for filtered stats:

```graphql
query {
  employeeGrouping(where: { salary: { gte: 80000 } }) {
    count
    aggregate { salary { avg } }
  }
}
```

## Hiding aggregates for specific fields

Use `GroupingConfig<T>` to ignore a field — see [Entity Configuration](../configuration/entity-configuration#overriding-with-groupingconfigt). The operations a field exposes are determined by its scalar type and the convention binding (`BindNumeric<T>()` vs `BindComparable<T>()`).
