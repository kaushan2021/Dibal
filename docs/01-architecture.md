# 01 — Architecture

## Stack

| Layer | Choice | Notes |
|---|---|---|
| Runtime | .NET 10 (LTS) | Pinned in `global.json` |
| Web | ASP.NET Core + Blazor | Per-page render modes |
| CSS | Tailwind standalone CLI | No Node in the build. Downloaded per-RID at build time, never committed |
| ORM | EF Core + Npgsql | `NpgsqlDataSource` registered as singleton |
| Database | Neon Postgres (free tier) | Pooled endpoint |
| Auth | ASP.NET Core Identity | Cookie auth, 3 roles |
| PDF | QuestPDF | Verify Community licence threshold applies |
| Email | MailKit | `System.Net.Mail.SmtpClient` is obsolete — do not use |
| Background | `BackgroundService` + transactional outbox | |
| Logging | Serilog | Console + rolling file |
| Object storage | Cloudflare R2 | S3-compatible; documents + backups |
| Hosting | Fly.io, 512 MB shared-cpu-1x, Amsterdam | Always-on |
| CI/CD | GitHub Actions | Build → migrate → deploy on merge to `main` |
| Staging | Render free tier | Cold starts acceptable here |

**Estimated running cost: ~£3/month.**

## Render mode policy

Static SSR is the default for the whole app. Enhanced navigation gives
SPA-feeling page transitions without a circuit. Interactivity is opt-in per page.

### Pages that are `InteractiveServer`

Only these. Adding to this list is an architecture decision, not a convenience.

- Goods-in bulk serial entry (live validation of 50+ serials)
- Delivery builder / serial allocation (stateful multi-step selection)
- App settings (SMTP test button, live validation)
- User management (invite/role forms)

### Everything else is static SSR

Client list and detail, stock list, serial lookup, delivery list and detail,
document archive, dashboard.

**Why**: these are the screens used on mobile. Static SSR is stateless, so a
dropped connection costs one retry rather than the session, and URLs are
bookmarkable and shareable.

### The failure mode to watch for

A statically-rendered component silently ignores `@onclick` and `@bind`. If a
button "does nothing", check `@rendermode` before debugging anything else.

## Stock as an append-only ledger

`stock_units.status` is a **cached projection**. The source of truth is
`stock_movements`, which is append-only.

```
Receipt      → InStock
Allocation   → Allocated      (delivery confirmed, not yet dispatched)
Delivery     → Delivered
Deallocation → InStock        (delivery cancelled — compensating movement)
Adjustment   → any            (manual correction, reason required)
WriteOff     → WrittenOff     (damaged/lost)
Return       → InStock/Faulty (reserved for future RMA, unused in v1)
```

Why a ledger rather than a mutable quantity: it gives audit trail,
reversibility, and "who changed this and when" for free, and it makes the
concurrency problem tractable (see below).

## Concurrency: allocating the last unit

Two users confirming deliveries that include the same serial must not both
succeed. Application-level checks are not sufficient.

Inside a single transaction:

1. `SELECT ... FROM stock_units WHERE id = ANY(@ids) FOR UPDATE`
2. Verify every unit is still `InStock`
3. Insert `stock_movements` rows
4. Update `stock_units.status` and `current_delivery_id`
5. Update `deliveries.status = 'Confirmed'`, write `client_snapshot`
6. Insert `outbox_messages` row if the user chose to email
7. Commit

If step 2 fails, the whole transaction rolls back and the second user gets a
clear "unit SN-xxxx was allocated by someone else" message.

## Transactional outbox

Email is never sent inline from a request handler. On delivery confirmation,
an `outbox_messages` row is written **in the same transaction** as the delivery.

`OutboxProcessor` (a `BackgroundService`) polls every 30s, claims pending
messages, sends via MailKit, and records the result in `email_log`.

Why: SMTP being slow or down must not block or fail the user's save. Either the
delivery and the queued email both commit, or neither does. Retries and
failure visibility fall out for free.

Retry: exponential backoff, max 5 attempts, then `Failed` with the error
visible in the UI and a manual resend button.

`Failed` is **terminal for the processor** but still listed in the UI, which is
why `ix_outbox_pending` covers it. The claim query must therefore filter
`attempts < 5` as well as `next_attempt_at <= now()` — otherwise a permanently
dead message is re-claimed on every 30-second poll, forever. Manual resend
resets `attempts` and `next_attempt_at`; that is what makes it retryable again.

## Search and performance

The realistic data volume is small — a few thousand deliveries per year. The
performance risk is **frontend architecture**, not the database.

Rules:

- Server-side pagination, filtering, and sorting on every list
- Keyset pagination (not `OFFSET`) on stock units and deliveries
- `pg_trgm` GIN indexes for fuzzy name and serial search
- `postcode_normalised` (generated column, uppercase, spaces stripped) for
  reliable postcode lookup
- Dashboard aggregates from a materialised view refreshed on a schedule —
  never `COUNT(*)` over everything on page load

## Build and deploy ordering

`ci-cd.yml` runs **build → migrate → deploy**. Migrations are applied before the
new image serves traffic, so a request never hits code that expects a column the
database does not have yet.

This assumes the migration is **additive** — a new table, a nullable column, an
index — which the old code tolerates for the length of one deploy. A
**destructive** migration breaks the assumption in the other direction: the
still-live old code reads the column being dropped. Split those across two
deploys — first ship code that no longer touches the column, then drop it in a
following PR. Do not reorder the jobs to work around it.

The image is built on GitHub Actions via `flyctl deploy --remote-only`, never on
a development machine (the Mac is ARM64, Fly is x86-64).

### Tailwind

The standalone CLI is downloaded for the current OS/architecture at build time
into a gitignored `tools/` directory, by `src/Dibal.Web/Tailwind.targets`. It is
deliberately **not** committed: this repo builds on an ARM64 Mac, a Windows
desktop and a linux-x64 container, and a single vendored binary would serve one
and break the other two. The version is pinned in the targets file — bump it as
a reviewed change.

## Backups

The database is on a free tier with a short restore window. **The external
backup is the real backup.**

1. Nightly GitHub Actions cron: `pg_dump` → gzip → GPG symmetric encrypt →
   Cloudflare R2
2. Retention via R2 lifecycle rules: 30 daily, 12 monthly
3. Generated PDFs live in R2 already; enable versioning on that bucket.
   **The nightly dump does not cover them.** A restored database therefore
   references `documents.storage_key` objects the dump does not own — the two
   halves are protected by different mechanisms and can drift. This is an
   accepted trade-off at this size, not an oversight; the restore drill should
   spot-check that a handful of `storage_key` values still resolve in R2
4. In-app: soft deletes + `audit_log` — this covers the most likely failure,
   which is human error, not infrastructure
5. **Quarterly restore drill into a scratch Neon branch.** An untested backup
   is not a backup. Calendar reminder, not code.

## Secrets

- Local: `dotnet user-secrets` per machine (does not sync — this is correct)
- Production: Fly.io secrets (`fly secrets set`)
- SMTP password: encrypted at rest in `app_settings` using ASP.NET Core
  Data Protection, keys persisted to a mounted volume. **Never plaintext.**

## Configuration that must be user-editable

Nothing about the email provider is hardcoded. `app_settings` holds host, port,
TLS mode, username, encrypted password, from-name, from-address, reply-to.
The settings screen has a **"send test email"** button — this is not optional,
it saves hours of debugging.
