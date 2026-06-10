---
title: Runtime Quirks
---

# Runtime Quirks

Provider-specific edge cases and surprising runtime behaviours. For deliberate API limitations see [Limitations](../architecture/limitations).

## Null vs. empty collection navigations look identical

**Data-model truth, not a library limitation.**

A relational one-to-many relationship has no notion of `null` on the owning row — related rows either exist or they don't. The `?` on `ICollection<Project>?` is a code-level artefact:

```csharp
new Employee { Projects = null }              // unloaded / not materialised
new Employee { Projects = [] }                // loaded, no rows
new Employee { Projects = [/* rows */] }      // loaded, has rows
```

All three of `Projects = null`, `Projects = []`, and "no related project rows in the DB" collapse to zero rows after `SelectMany`, so the owning row is absent from `employeeGrouping { key { projects { name } } }` either way.

Single-valued navigations (one-to-one, many-to-one) **are** different — their FK column can be `NULL`, which surfaces as a `null` bucket (see [Grouping → Distinguishing ancestor-null from leaf-null](../examples/grouping#distinguishing-ancestor-null-from-leaf-null)).

**Workarounds**: treat `null` and `[]` as the same. To find "owning rows with no related entries", query separately with a collection filter like `where: { projects: { any: false } }`. `filterNullParent: true` doesn't help — the `SelectMany` already discarded the rows.

## Additive aggregates over a collection key fan out

When `key` crosses a collection navigation the source is `SelectMany`-flattened first, and every aggregate runs on those flattened rows. An employee with three projects contributes three rows — her salary is counted three times. That's deliberate; it lets you weight a parent value per child when you want to. But **summing a parent attribute through a collection is almost never what you want** — the answer diverges from the per-parent total as soon as any parent has multiple matching children.

Worked example: Alice earns 100k and works on `Alpha` + `Beta`.

```graphql
query {
  employeeGrouping {
    key { projects { name } }
    aggregate { salary { sum } }
  }
}
```

The `Alpha` bucket's `salary.sum` includes Alice's 100k once (her one Alpha row); the `Beta` bucket's `salary.sum` also includes 100k. Summing `salary.sum` across the two buckets double-counts her — the total is **not** the salary of employees who work on Alpha or Beta.

If you want per-parent stats, drop the collection from `key`:

```graphql
query {
  employeeGrouping {
    key { department { name } }      # no collection in the key
    aggregate { salary { sum } }     # per-employee — no fan-out
  }
}
```

`count` follows the same rule: with `key { projects { name } }` it counts flattened (employee, project) rows, not distinct employees.

### Sibling collections multiply the fan-out

Selecting two collections in `key` that aren't nested in each other flattens both — every combination of elements becomes a row, exactly as two SQL `JOIN`s produce a cross product:

```graphql
{
  employeeGrouping {
    key {
      projects { name }   # 3 projects ×
      skills { name }     # 2 skills = 6 rows per employee
    }
    count                 # counts (project, skill) pairs, not employees
  }
}
```

Counts multiply and additive aggregates repeat per combination, so the double-counting caveat above applies with even more force. Group by one collection per request, or nest the second collection under the first if the model relates them.

## Decimal precision drift across providers

The same query may return slightly different `avg` values on high-precision decimals:

```json
{ "avg": { "bonus": 5166.6666666666666666666666667 } }   // in-memory
{ "avg": { "bonus": 5166.67 } }                         // SQLite (truncates to 2dp)
```

**Workaround**: round client-side or store as integer cents.

## EF Core: `Average` over an empty bucket returns null, not the documented `0`

C# `Average()` throws on empty collections; LINQ-to-SQL returns `null`. The library wraps the projection so `avg` returns `null` for empty buckets uniformly across all three providers.

## HAVING null comparisons are explicit, not lifted

`eq: null` / `neq: null` emit explicit `IS NULL` / `IS NOT NULL` (not lifted equality, which would diverge between in-memory and EF). `nin: [...]` adds a `agg != null` guard so a null aggregate slot never satisfies `NOT IN` — matches SQL semantics consistently across all three providers.
