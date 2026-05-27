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

## `count` over a collection key counts join rows, not parents

SQL `JOIN` semantics. When `key` crosses a collection navigation, the source is `SelectMany`-flattened — an employee with two projects contributes two rows. `count` and `sum` are over flattened rows, not parents.

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
