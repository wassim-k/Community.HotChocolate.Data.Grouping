---
title: Middleware Pipeline
---

# Middleware Pipeline

A grouping request through the HotChocolate middleware chain.

## Pipeline placement

```csharp
[UseGrouping]
[UseFiltering]
public IQueryable<Employee> GetEmployeeGrouping(MyDbContext db) => db.Employees;
```

```
source (IQueryable<Employee>)
  → UseFiltering: applies `where`              (SQL: WHERE before GROUP BY)
  → UseGrouping:  filterNullParent + GroupBy + project + having → grouping result
  → Resolver:     returns to the client
```

`[UseSorting]` is not compatible — see [Limitations](../architecture/limitations#usesorting-is-not-compatible).

## Phases

### 1. Schema build

`UseGrouping()` wires up the middleware, a `filterNullParent: Boolean = <default>` argument (default from the convention), a rewrite of the field's return type to `[<Entity>Grouping!]!`, and generation of `<Entity>Grouping`/`<Entity>GroupingKey`/`<Entity>Aggregate` plus the per-scalar `*AggregateResult` types pulled from the convention's bindings. Each aggregate operation's `having:` argument references HotChocolate.Data's `*OperationFilterInput`. The same `<Entity>Aggregate` is reused under every navigation.

### 2. Selection parsing

The middleware reads the GraphQL selection set under `key` and `aggregate` to build:

- A `SelectionPlan` of key paths, aggregate paths, and the operations requested on each leaf.
- A list of `HavingPredicate`s — one per operation that carried a `having` argument.

Each path segment is pre-classified (scalar, navigation, collection) so the runtime can dispatch quickly.

### 3. Query construction

```
source → SelectMany?... → Where? → GroupBy → Select → Where? → grouping result
```

- `SelectMany` once per collection navigation in the plan.
- The pre-grouping `Where` only when `filterNullParent: true` produces actual parent null-checks.
- `GroupBy` + `Select` project key and aggregates into a carrier row.
- A second `Where` appends when any operation carried `having` — predicates translate against the projected carrier and AND-combine.

The same tree feeds whatever LINQ provider the source belongs to.

### 4. Execution

`IGroupingProvider.ApplyAsync<T>(source, plan, filterNullParent, having, context, cancellationToken)` runs the composed query — a single round-trip for SQL/Mongo, enumerator-walk for in-memory. Providers that don't recognise the source shape return `null`, leaving the resolver result untouched.

### 5. Resolution

Schema-type resolvers walk the grouping result by GraphQL field name to populate `key` and `aggregate`.

## Error handling

| Origin | Surfaced as |
|---|---|
| `NotSupportedException` from the LINQ provider | `GraphQLException` with code `GROUPING_NOT_SUPPORTED`, original message, `Path` + `Location` |
| Any other exception escaping the provider | `GraphQLException` with code `GROUPING_RESOLVER_FAILED`; original exception on `error.Exception` |
| Schema-build error | Raised at schema build, before request execution |

No error falls into HotChocolate's generic "Unexpected Execution Error" wrapper.

## Provider notes

Tested against in-memory LINQ, EF Core / SQLite, and MongoDB.Driver.Linq. The expression tree is identical across all three; providers may emit different SQL or aggregation pipelines and may differ on null/precision semantics.
