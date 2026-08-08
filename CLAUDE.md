# CLAUDE.md

Instructions for AI coding assistants working in this repository.
Read this before making any change. These are invariants, not preferences.

---

## What this is

An internal back-office system for a weighing-scale distributor. Tracks client
companies, serialised stock, deliveries, warranty/support periods, and generated
documents. 3–5 internal users. Not customer-facing.

Full context in `docs/00-overview.md`. Architecture in `docs/01-architecture.md`.
Schema in `docs/02-schema.sql`. Phase plan in `docs/03-phases.md`.

---

## Hard invariants

Violating any of these is a bug even if the code compiles and tests pass.

### 1. Never mutate `stock_units.status` directly

Every status change MUST insert a row into `stock_movements` in the same
transaction. `stock_units.status` is a cached projection of the last movement,
never an independently-writable field.

```csharp
// WRONG
unit.Status = StockStatus.Delivered;
await db.SaveChangesAsync();

// RIGHT
await stockService.RecordMovementAsync(
    unit, MovementType.Delivery, StockStatus.Delivered,
    deliveryId: delivery.Id, performedBy: userId);
```

`stock_movements` is append-only. Never UPDATE or DELETE a movement row.
Corrections are new compensating movements.

### 2. Never hard-delete

All user-facing entities use soft delete (`is_deleted`) with EF Core global
query filters. There is no scenario in this application where a
`Remove()` on a client, delivery, stock unit, or document is correct.

An abandoned draft delivery is **cancelled**, not deleted. That is why
`ck_confirmed_has_snapshot` permits `Cancelled` without a `client_snapshot` —
a draft never had one to freeze.

Exceptions, and only these two:

- `outbox_messages` may be purged after successful send + 30 days.
- `delivery_lines` on a **Draft** delivery may be hard-deleted. Unticking a
  serial in the delivery builder is editing a basket, not destroying a record,
  and `delivery_lines` has no soft-delete column for that reason. **Once the
  delivery is Confirmed its lines are immutable** — removing a unit from a
  confirmed delivery means cancelling the delivery, which returns the units to
  stock via compensating movements.

### 3. Every Razor page declares its render mode explicitly

Static SSR is the default. Interactivity is opt-in per page.

```razor
@rendermode InteractiveServer
```

A statically-rendered component silently ignores `@onclick`. If a handler
"does nothing", check the render mode first.

Interactive pages are listed in `docs/01-architecture.md`. Do not add
`InteractiveServer` to a page not on that list without flagging it.

**A static page that needs to do something uses an HTML form POST**
(`<EditForm FormName="..." OnValidSubmit="...">` with `[SupplyParameterFromForm]`),
never `@onclick`. Adding `@rendermode InteractiveServer` to make a button work is
the wrong fix — it puts a SignalR circuit on a page that was deliberately
stateless.

The case this will come up first: **the resend-document button on delivery
detail.** `docs/00-overview.md` requires that page to work on a phone, and
`docs/01-architecture.md` keeps it static SSR. Both hold — as a form POST.

### 4. Migration protocol

- ALWAYS `git pull` before `dotnet ef migrations add`
- NEVER edit a migration that has been applied to any environment
- One migration per logical change, named descriptively
- Review generated SQL before committing — EF Core's inference is not always right

This repo is worked on from two machines. Two migrations created in parallel
produce conflicting parent IDs and a painful merge.

### 5. Search is always server-side

Never fetch a full table into memory and filter in C#/JS. All list screens use
server-side pagination, filtering, and sorting. Use keyset pagination for
anything that can exceed a few thousand rows.

### 6. Documents are immutable

Once a `documents` row exists, its PDF in object storage is never regenerated
or overwritten. A delivery note reflects the client address *at time of
generation*, which is why `deliveries.client_snapshot` exists. Reprinting means
re-serving the stored file, not re-rendering it.

### 7. Email goes through the outbox

Never call MailKit directly from a request handler. Write an
`outbox_messages` row inside the same transaction as the business change.
The `OutboxProcessor` background service sends it.

---

## Conventions

- **Solution layout**: `src/Dibal.Domain` (entities, no dependencies),
  `src/Dibal.Infrastructure` (EF Core, storage, email), `src/Dibal.Web`
  (Blazor, Identity), `tests/`
- **Domain project has no EF Core dependency.** Persistence concerns stay in
  Infrastructure.
- **Money**: none. This system has no prices, VAT, or payments. Do not add them.
- **Dates**: `DateOnly` for calendar dates (received, warranty, support),
  `DateTimeOffset` for events (occurred_at, created_at). All UTC in the database.
- **IDs**: UUID v7 for entities, `bigserial` for append-only logs.
- **Warranty/support status is ALWAYS computed from dates.** Never store a
  Valid/Expired column.
- **Nullable reference types enabled.** No `!` suppression without a comment
  explaining why.
- **Tailwind only.** No inline styles, no additional CSS frameworks. The CLI is
  downloaded per-platform at build time by `src/Dibal.Web/Tailwind.targets` —
  never commit the binary.
- **Postgres enums must be mapped on the data source.** The schema defines eight
  native enum types. Each needs `MapEnum<T>()` on the `NpgsqlDataSourceBuilder`
  at registration, or every query touching that column fails at runtime with an
  unmapped-type error rather than at compile time. Adding an enum value is a
  migration (`ALTER TYPE ... ADD VALUE`), not just a C# change.
- **The outbox claim query filters `attempts < 5`.** `Failed` is terminal for
  `OutboxProcessor` but still indexed for the UI; without that filter dead
  messages are re-claimed on every poll.

## Things that are deliberately out of scope for v1

Do not add these without being asked:

- Returns / RMA / warranty claims workflow (schema supports it; UI does not)
- Prices, invoicing, VAT, payment tracking
- Multi-warehouse / multi-location stock
- Customer-facing portal
- Multi-currency

The enum values `Returned`, `Faulty`, `WriteOff` exist in the schema so RMA can
be added later as a UI change rather than a migration. Leave them.

## Before you finish a task

- Does it touch stock status? Did it write a movement row?
- Does it delete anything? Should it be a soft delete?
- Is there a new list screen? Is it paginated server-side?
- New page? Is the render mode declared?
- New migration? Did you pull first?
