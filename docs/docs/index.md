---
slug: /
sidebar_position: 0
sidebar_label: Introduction
---

# Community.HotChocolate.Data.Grouping

A type-safe GROUP BY aggregation library for [HotChocolate](https://chillicream.com/docs/hotchocolate) GraphQL.

Grouping enables SQL `GROUP BY` semantics directly in your GraphQL schema.

The `key` selection set defines the grouping dimensions, `aggregate` carries the per-bucket numbers, and `having` filters the buckets — all in one round-trip, translated to a single database query.

```graphql
query {
  employeeGrouping(where: { active: { eq: true } }) {       # filter rows before GROUP BY
    key {
      company { name }
      department { name }
    }
    count(having: { gt: 1 })                                # drop singleton buckets
    aggregate {
      salary { avg sum min max(having: { gte: 90000 }) }    # keep buckets whose max salary ≥ 90k
      bonus  { avg }
    }
  }
}
```

```json
{
  "data": {
    "employeeGrouping": [
      {
        "key": { "company": { "name": "Acme" }, "department": { "name": "Engineering" } },
        "count": 2,
        "aggregate": {
          "salary": { "avg": 90000, "sum": 180000, "min": 80000, "max": 100000 },
          "bonus":  { "avg": 10000 }
        }
      },
      {
        "key": { "company": { "name": "Globex" }, "department": { "name": "Engineering" } },
        "count": 2,
        "aggregate": {
          "salary": { "avg": 92500, "sum": 185000, "min": 90000, "max": 95000 },
          "bonus":  { "avg": 5000 }
        }
      }
    ]
  }
}
```

The library follows familiar SQL semantics on top of any `IQueryable` — `count` is always the row count of the bucket, putting a collection in `key` flattens like a `JOIN`, and the schema rejects combinations that would silently change which buckets you get back.

## Quick Start

```csharp {4,9}
services
    .AddGraphQL()
    .AddFiltering()
    .AddGrouping()
    .AddQueryType<Query>();

public class Query
{
    [UseGrouping]
    [UseFiltering]
    public IQueryable<Employee> GetEmployeeGrouping(MyDbContext db) => db.Employees;
}
```

That's it — the schema gains `employeeGrouping(where, filterNullParent)` returning `[EmployeeGrouping!]!` with `key` + `aggregate`. See [Quick Start](./getting-started/quick-start) for the walkthrough or [Design Decisions](./architecture/design-decisions) for the rationale.


## Key Features

- **Schema-driven type safety** — every operation a field exposes and every `having` predicate is determined by its scalar type; invalid combinations are unrepresentable in the schema rather than discovered at runtime
- **Selection-driven** — `key { … }` defines the grouping dimensions; no `groupBy` argument required
- **Field-first aggregates** — `salary { avg sum min max }`
- **Per-operation HAVING** — `salary { sum(having: { gt: 100000 }) }` filters buckets server-side
- **Nested grouping** — group by navigation properties, including across collection navigations
- **Multi-source** — any `IQueryable` provider; in-memory, EF Core, and MongoDB are tested against every query
- **Convention-based** — custom scalar types registered via a fluent convention API
