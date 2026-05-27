---
title: Design Decisions
---

# Design Decisions

The reasoning behind the architectural choices.

## 1. SQL semantics, on top of any `IQueryable`

The single guiding principle: a grouping query should behave the way SQL `GROUP BY` would. The same query against in-memory `IEnumerable`, EF Core, MongoDB, or any future `IQueryable` provider must produce the same numbers and buckets.

Concretely:

- **`<Grouping>.count` is always "rows in the bucket."** It sits as a sibling of `key` / `aggregate` (not inside `aggregate`), and never silently changes meaning depending on what other aggregates are selected.
- **A collection in `key` flattens the source** — `SELECT … FROM employees JOIN projects … GROUP BY projects.name`. Each `(employee, project)` pair is a row; `count` counts pairs.
- **A collection in `aggregate` requires the same collection in `key`.** Otherwise the join would happen silently, drop entities without that collection, and turn `count` into a flattened-row count without the user asking for it. Rejected at parse time with `GROUPING_AGGREGATE_COLLECTION_MISSING_FROM_KEY` — see [Limitations → Aggregating over a collection](../architecture/limitations#aggregating-over-a-collection).
- **No correlated-subquery magic.** Some engines (notably Hasura) emit per-relationship subqueries to keep sibling aggregates independent. We deliberately don't — it forces provider-specific knowledge. The cost is honest: sibling collections inflate each other under JOIN semantics, exactly as SQL would.

## 2. Selection-driven grouping, not an argument

The selection set under `key` defines the dimensions, instead of a `groupBy: { ... }` input argument.

```graphql
# selection-driven (this library)
employeeGrouping {
  key { company { name }  department { name } }
  count
}

# argument-driven (rejected)
employeeGrouping(groupBy: { department: { name: true }  company: { name: true } }) {
  ...
}
```

The shape that names the dimensions also shapes the response — the bucket's `key` echoes the request literally. Nested navigations and collection flattening use existing GraphQL syntax, with no parallel "groupBy input" hierarchy.

## 3. Every column is either a `key` dimension or an `aggregate`

A bucket exposes exactly `key` and `aggregate`. There's no slot for a "loose" non-aggregated column.

This isn't a LINQ limitation — standard SQL, SQL Server, and MySQL under `ONLY_FULL_GROUP_BY` all require every `SELECT` column to be in `GROUP BY` or wrapped in an aggregate. The schema makes this a structural invariant so a query valid against the in-memory provider is also valid against SQL Server.

## 4. Field-first aggregates

`salary { avg sum min max }` rather than `avg { salary }`. `count` is a peer of `aggregate` on `*Grouping`, not inside it.

- **The scalar's type controls valid operations.** `IntAggregateResult` exposes `avg sum min max`; `StringAggregateResult` exposes `min max`. Invalid combinations are unrepresentable rather than discovered at runtime.
- **`having` is per-operation.** `salary { sum(having: { gt: 100000 }) }` — the argument lives on the operation it filters, with an input type matching that operation's result scalar.
- **One `*AggregateResult` per scalar, one `*Aggregate` per entity.** The per-entity `*Aggregate` is reused at every nesting level — adding an entity adds no aggregate-result types.

Trade-off: four operations on one field requires a sub-selection. Accepted for the schema clarity.

## 5. Selection-set translation, not in-memory grouping

The middleware composes a single LINQ expression tree and hands it to the provider; it never iterates rows. The same code path is correct for in-memory, SQL, and Mongo. See [Middleware Pipeline](./middleware-pipeline).

Cost: anything the provider can't translate fails at runtime, not at schema build.

## 6. Pre-defined `AnonymousType<...>` carriers

The library ships `AnonymousType<T1>` through `AnonymousType<T1, …, T16>` — generic records that look enough like compiler-generated anonymous types for EF Core and MongoDB.Driver.Linq to translate `.ItemN` access into native SQL/Mongo. They give value-based equality (so `GroupBy` buckets correctly) without runtime code generation, keeping AOT compatibility. Capped at 16 fields — see [Limitations → Max 16-field selection per group or aggregate](../architecture/limitations#max-16-field-selection-per-group-or-aggregate).

## 7. Why `Grouping`, not `Group`?

`Group` collides with common domain identifiers (`UserGroup`, `OrgGroup`). `Grouping` matches the verb-form pattern HotChocolate.Data uses elsewhere (`FilterInput`). The suffixes are stable across versions and part of the public GraphQL surface.

## 8. HAVING as a per-operation argument

`having` is a per-operation argument whose input type is HotChocolate.Data's `*OperationFilterInput` for the operation's (widened) result scalar — not a separate per-entity HAVING input. Same motivation as field-first aggregates: a predicate on `salary.sum` is semantically attached to that operation. Predicates from multiple `having` arguments AND-combine. Filter-side concerns (operators, handlers, type bindings) go through `AddFiltering` — see [HAVING Filtering](../configuration/having-filtering#having-needs-addfiltering).
