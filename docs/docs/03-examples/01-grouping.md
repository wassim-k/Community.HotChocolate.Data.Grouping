---
title: Grouping
---

# Grouping

Examples run against the test suite's in-memory `Employee` dataset.

| Name  | Active | Salary  | Bonus  | Company | Department.Name | Projects             |
|-------|--------|---------|--------|---------|-----------------|----------------------|
| Alice | true   | 100,000 | 10,000 | Acme    | Engineering     | Alpha, Beta          |
| Bob   | true   |  80,000 | (null) | Acme    | Engineering     | Alpha                |
| Carol | true   | 120,000 | 20,000 | Acme    | Sales           | Gamma, Delta, Beta   |
| Dave  | true   |  90,000 |  5,000 | Globex  | Engineering     | Alpha                |
| Eve   | true   |  95,000 | (null) | Globex  | Engineering     | (null)               |
| Frank | false  |  75,000 |  7,500 | Globex  | Sales           | []                   |
| Grace | false  |  60,000 | (null) | Acme    | *Department=null* | Alpha              |
| Henry | false  |  85,000 |  3,000 | Globex  | (Dept exists, *Name=null*) | Beta      |

## Key shapes

### Scalar / single navigation

```graphql
query {
  employeeGrouping {
    key { department { name } }
  }
}
```

```json
[
  { "key": { "department": { "name": "Engineering" } } },
  { "key": { "department": { "name": "Sales" } } },
  { "key": { "department": { "name": null } } }
]
```

`count` is a peer of `key` / `aggregate` and is always available — see [Aggregations](./aggregations).

### Multiple key fields + COUNT

```graphql
query {
  employeeGrouping {
    key {
      company { name }
      department { name }
    }
    count
  }
}
```

```json
[
  { "key": { "company": { "name": "Acme" },   "department": { "name": "Engineering" } }, "count": 2 },
  { "key": { "company": { "name": "Globex" }, "department": { "name": "Engineering" } }, "count": 2 },
  { "key": { "company": { "name": "Acme" },   "department": { "name": "Sales" } },       "count": 1 },
  { "key": { "company": { "name": "Globex" }, "department": { "name": "Sales" } },       "count": 1 },
  { "key": { "company": { "name": "Acme" },   "department": { "name": null } },          "count": 1 },
  { "key": { "company": { "name": "Globex" }, "department": { "name": null } },          "count": 1 }
]
```

Drill arbitrarily deep — anything the LINQ provider can translate to a `JOIN` works.

### Distinguishing ancestor-null from leaf-null

A single nullable navigation can't say whether the *parent* was null or the leaf. Grace (`Department = null`) and Henry (`Department.Name = null`) both produce `{ "name": null }`. Select the FK alongside the navigation to separate them:

```graphql
query {
  employeeGrouping {
    key {
      departmentId
      department { name }
    }
    count
  }
}
```

```json
[
  { "key": { "departmentId": 1,    "department": { "name": "Engineering" } }, "count": 2 },
  { "key": { "departmentId": 5,    "department": { "name": null } },          "count": 1 },
  { "key": { "departmentId": null, "department": { "name": null } },          "count": 1 }
]
```

## Collection-navigation keys

A `key` path crossing a collection navigation inserts a `SelectMany` before grouping — each parent row contributes one row per element (SQL `JOIN` semantics).

```graphql
query {
  employeeGrouping {
    key { projects { name } }
    count
  }
}
```

```json
[
  { "key": { "projects": { "name": "Alpha" } }, "count": 4 },
  { "key": { "projects": { "name": "Beta" } },  "count": 3 },
  { "key": { "projects": { "name": "Delta" } }, "count": 1 },
  { "key": { "projects": { "name": "Gamma" } }, "count": 1 }
]
```

`count` reflects the flattened row count, not distinct employees.

:::warning[Null and empty collections look identical]
Eve (`Projects = null`) and Frank (`Projects = []`) **both** drop out — a one-to-many relationship has no notion of `null` on the owning row. See [Runtime Quirks](../architecture/runtime-quirks#null-vs-empty-collection-navigations-look-identical).
:::

### Mixed scalar + collection key

```graphql
query {
  employeeGrouping {
    key {
      projects { name }
      company { name }
    }
    count
  }
}
```

```json
[
  { "key": { "projects": { "name": "Alpha" }, "company": { "name": "Acme" } },   "count": 3 },
  { "key": { "projects": { "name": "Alpha" }, "company": { "name": "Globex" } }, "count": 1 }
]
```

Aggregates over a collection-keyed group run on flattened rows, not entities — see [Aggregations → Aggregating across a collection key](./aggregations#aggregating-across-a-collection-key).

## `filterNullParent`

`filterNullParent: true` drops rows whose parent navigation chain contains any null **before** the GROUP BY. For `key { department { name } }` the row stays only if `department` is non-null; for a deeper path like `department { manager { name } }`, both `department` and `department.manager` must be non-null. Grace disappears (her `Department` is null); Henry stays (his `Department` exists, only its `Name` is null).

```graphql
query {
  employeeGrouping(filterNullParent: true) {
    key { department { name } }
  }
}
```

With the filter on, a `null` in `key.department.name` means one thing only: the `Name` itself is null. It never means the employee has no `Department`. You can take that value and use it in a sibling filter — `{ department: { name: { eq: <keyValue> } } }` — and get back the same rows the bucket reported.

Without it, the same query is risky. With `eq` or `neq`, a `null` key also matches employees who have no `Department` at all, so you get more rows than the bucket had. With `contains`, `startsWith`, or `endsWith`, the query fails with `HC0026 — "Null values are not supported"`.

How it works: each leaf path in `key` requires its full parent chain to be non-null before the GROUP BY. Paths through a flattened collection, or paths with no parent at all, skip the check. Aggregate paths aren't filtered — `SUM` and `AVG` already skip nulls, and filtering them would shrink `count` without you asking.

## Filtering with `where`

`[UseGrouping]` composes with `[UseFiltering]`. `where` filters source rows **before** the GROUP BY — same as SQL `WHERE`.

```csharp
public class Query
{
    [UseGrouping]
    [UseFiltering]
    public IQueryable<Employee> GetEmployeeGrouping(MyDbContext db) => db.Employees;
}
```

```graphql
query {
  employeeGrouping(where: { salary: { gte: 80000 } }) {
    key { department { name } }
    count
    aggregate { salary { avg } }
  }
}
```

Frank (`75k`) and Grace (`60k`) are excluded before grouping. Filters compose on navigations (`where: { company: { name: { eq: "Acme" } } }`). `where` and `filterNullParent` are independent and both apply pre-GROUP BY.

For post-grouping bucket filtering use the per-operation `having` argument — see [Aggregations → HAVING](./aggregations#filtering-buckets-with-having).

`[UseSorting]` is **not** compatible — see [Limitations](../architecture/limitations#usesorting-is-not-compatible).
