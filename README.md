<div align="center">

<img src="https://raw.githubusercontent.com/wassim-k/Community.HotChocolate.Data.Grouping/main/assets/icon.png" alt="Community.HotChocolate.Data.Grouping" width="120" />

# Community.HotChocolate.Data.Grouping

[![NuGet](https://img.shields.io/nuget/v/Community.HotChocolate.Data.Grouping.svg?logo=nuget&label=NuGet)](https://www.nuget.org/packages/Community.HotChocolate.Data.Grouping)
[![Downloads](https://img.shields.io/nuget/dt/Community.HotChocolate.Data.Grouping.svg?logo=nuget&label=Downloads)](https://www.nuget.org/packages/Community.HotChocolate.Data.Grouping)
[![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%209.0%20%7C%2010.0-512BD4?logo=dotnet)](#)
[![HotChocolate](https://img.shields.io/badge/HotChocolate-16%2B-blueviolet)](https://chillicream.com/docs/hotchocolate)
[![Docs](https://img.shields.io/badge/docs-online-2ea44f?logo=docusaurus)](https://wassim-k.github.io/Community.HotChocolate.Data.Grouping/)

[📖 **Documentation**](https://wassim-k.github.io/Community.HotChocolate.Data.Grouping/) &nbsp;·&nbsp; [🚀 Quick Start](https://wassim-k.github.io/Community.HotChocolate.Data.Grouping/getting-started/quick-start) &nbsp;·&nbsp; [📦 NuGet](https://www.nuget.org/packages/Community.HotChocolate.Data.Grouping) &nbsp;·&nbsp; [🐛 Issues](https://github.com/wassim-k/Community.HotChocolate.Data.Grouping/issues)

</div>

---

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

```bash
dotnet add package Community.HotChocolate.Data.Grouping
```

```csharp
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

That's it — the schema gains `employeeGrouping(where, filterNullParent)` returning `[EmployeeGrouping!]!` with `key` + `aggregate`. See the [Quick Start](https://wassim-k.github.io/Community.HotChocolate.Data.Grouping/getting-started/quick-start) for a full walkthrough.

## Key Features

- **Schema-driven type safety** — every operation a field exposes and every `having` predicate is determined by its scalar type; invalid combinations are unrepresentable in the schema rather than discovered at runtime
- **Selection-driven** — `key { … }` defines the grouping dimensions; no `groupBy` argument required
- **Field-first aggregates** — `salary { avg sum min max }`
- **Per-operation HAVING** — `salary { sum(having: { gt: 100000 }) }` filters buckets server-side
- **Nested grouping** — group by navigation properties, including across collection navigations
- **Multi-source** — any `IQueryable` provider; in-memory, EF Core, and MongoDB are tested against every query
- **Convention-based** — custom scalar types registered via a fluent convention API
