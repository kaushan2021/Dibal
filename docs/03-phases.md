# 03 — Phase plan

Each phase has an exit criterion. Do not start the next phase until it is met.
Phases are ordered by dependency, not by visible progress.

---

## Phase 0 — Foundations

No features. The goal is that everything after this is just writing screens.

### Repo and tooling
- [ ] Private repo, .NET + JetBrains `.gitignore`
- [ ] `global.json` pinning the exact SDK version
- [ ] `.gitattributes` with `* text=auto eol=lf`
- [ ] `.editorconfig` matched to Rider defaults
- [ ] Solution: `src/Dibal.Domain`, `src/Dibal.Infrastructure`,
      `src/Dibal.Web`, `tests/Dibal.Tests`
- [ ] Tailwind standalone CLI wired into the build via
      `src/Dibal.Web/Tailwind.targets` (downloads per-RID; binary not committed)
- [ ] `Dockerfile` — multi-stage, non-root, port 8080. Referenced by `fly.toml`;
      nothing deploys without it
- [ ] `README.md` documenting two-machine setup and required user-secrets keys

### Infrastructure
- [ ] Neon project with `dev` and `prod` branches; pooled connection strings
- [ ] Fly.io app, `fly.toml` with `auto_stop_machines = false`, spend alert set
- [ ] Cloudflare R2 buckets `documents` (versioning on) and `backups`
- [ ] GitHub Actions: build + test on PR; build image and deploy on merge to `main`
- [ ] `/health` endpoint + UptimeRobot check

### Application skeleton
- [ ] EF Core + Npgsql; `NpgsqlDataSource` as singleton
- [ ] ASP.NET Core Identity; roles Owner / Manager / Staff; root account
      seeded from environment variables
- [ ] `MapEnum<T>()` registered on the `NpgsqlDataSourceBuilder` for all eight
      Postgres enum types
- [ ] Initial migration seeds the `app_settings` singleton row (`id = 1`) —
      `stock_levels` reads its low-stock threshold from it
- [ ] `SaveChanges` interceptor writing `audit_log` with jsonb before/after
- [ ] Global query filters for soft delete
- [ ] Serilog to console + rolling file
- [ ] Base layout in Tailwind
- [ ] One static-SSR page and one `InteractiveServer` page, both deployed and
      working, to prove the render-mode setup end to end

### Data safety
- [ ] Nightly `pg_dump` → gzip → GPG encrypt → R2 via GitHub Actions cron
- [ ] R2 lifecycle rules: 30 daily, 12 monthly
- [ ] `CLAUDE.md` in place at repo root

**Exit criteria — all four:**
1. A commit to `main` deploys to Fly automatically
2. Both render modes work on the deployed app
3. The nightly backup has run **and been restored** into a scratch Neon branch
4. The repo builds from a fresh clone on both the Mac and the Windows machine

---

## Phase 1 — Client directory

Smallest independently useful slice. Real data goes in at the end of it.

- [ ] `clients` table + EF entity
- [ ] List with server-side pagination, sorting, and search across business
      name, client name, postcode
- [ ] `pg_trgm` fuzzy matching so typos and partial names still find the record
- [ ] Create / edit / soft-delete, Manager+ only
- [ ] Detail page showing the audit trail
- [ ] Customer type filter (Reseller / EndUser)
- [ ] Mobile layout for list and detail

**Exit:** the real client list is imported and any client is findable in under
two seconds by name, partial name, or postcode.

---

## Phase 2 — Stock

- [ ] `scale_models` CRUD with size band and optional low-stock override
- [ ] `goods_in_batches` + bulk serial entry (`InteractiveServer`):
      paste newline-separated list, plus serial-range generation
      (`SN-1001` → `SN-1060`), with per-serial duplicate validation
- [ ] `stock_units` + `stock_movements` ledger and `StockService`
- [ ] Manual stock adjustment / write-off with mandatory reason
- [ ] Stock list filtered by In stock / Low stock / Out of stock, per model
- [ ] **Serial lookup screen** — search a serial, see the unit, its model,
      current status, owning client if delivered, warranty and support status,
      and full movement history

**Exit:** real current stock is loaded and the serial lookup screen answers
"who has SN-xxxx and is it in warranty" in one search.

The serial lookup screen is the payoff for serialised inventory. Build it well.

---

## Phase 3 — Deliveries

- [ ] `deliveries`, `delivery_lines`, `delivery_counters`
- [ ] Reference numbering (`DN-2026-0001`) claimed with `SELECT ... FOR UPDATE`
- [ ] Delivery builder (`InteractiveServer`): pick client, search available
      units, tick serials onto the delivery, running count
- [ ] Draft → Confirmed transition: single transaction with row locking,
      writes movements, sets `client_snapshot`
- [ ] Confirmed → Dispatched
- [ ] Cancellation: compensating movements return units to stock; the delivery
      is never deleted
- [ ] Warranty and support dates defaulted from the header, editable per line
- [ ] Support renewal action writing `support_renewals`
- [ ] Delivery list and detail, searchable by business name, client name,
      postcode, reference

**Exit:** two concurrent users cannot allocate the same serial. Test this
deliberately — two browsers, same unit, confirm simultaneously.

---

## Phase 4 — Documents

- [ ] QuestPDF delivery/warranty note template with company branding
- [ ] Renders from `client_snapshot`, never from the live client row
- [ ] Upload to R2, store `storage_key` + `sha256` + size in `documents`
- [ ] Download / reprint from the delivery detail page
- [ ] Document archive screen, searchable

**Exit:** a document generated today is byte-identical when retrieved after the
client's address has been changed.

---

## Phase 5 — Email

- [ ] SMTP settings in `app_settings`, password encrypted via Data Protection
- [ ] **Send test email** button in settings
- [ ] `outbox_messages` written in the delivery-confirmation transaction
- [ ] `OutboxProcessor` background service: poll, send via MailKit,
      exponential backoff, max 5 attempts
- [ ] `email_log` with failure visibility and a manual resend button
- [ ] On confirmation the user chooses: download PDF, or email to client

**Exit:** a failed send is visible in the UI and retryable. Nothing fails silently.

---

## Phase 6 — Dashboard and alerts

- [ ] `stock_levels` materialised view, refreshed on schedule and after
      goods-in / confirmation
- [ ] Tiles: total in stock, low-stock models, out-of-stock models,
      deliveries this month, support expiring in 30 days
- [ ] Support expiry list (30 / 60 / 90 days) — this is renewal revenue
- [ ] Recent activity feed from `audit_log`

---

## Phase 7 — Users and settings

- [ ] User list, invite, disable, role assignment (Owner only)
- [ ] Company profile and document branding
- [ ] Reference numbering configuration
- [ ] Global low-stock threshold

---

## Phase 8 — Hardening and go-live

- [ ] Tailwind transitions and View Transitions API polish
- [ ] Empty states, error states, loading states on every screen
- [ ] Keyboard shortcuts on the high-traffic screens
- [ ] CSV export for clients, stock, deliveries
- [ ] Desktop-only guard panels on goods-in and delivery builder
- [ ] Quarterly restore drill added to a calendar
- [ ] Data migration from the current spreadsheets
- [ ] Cutover

---

## Notes on sequencing

- Stock comes before deliveries because deliveries depend on it.
- Backups are in Phase 0, not Phase 8, because that is where they normally get
  forgotten.
- Phase 6 alerts are deferred because support-expiry reminders are only useful
  once real dates exist in the system.
- Phase 1 ends with real data going in. Every phase after that is tested against
  reality rather than fixtures.
