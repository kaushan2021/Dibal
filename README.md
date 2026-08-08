# Dibal

Internal back-office system for a weighing-scale distributor: client directory,
serialised stock, deliveries, warranty/support tracking, and document generation.

Start with [`CLAUDE.md`](CLAUDE.md) — it holds the architectural invariants.

| Document | What's in it |
|---|---|
| [`docs/00-overview.md`](docs/00-overview.md) | Domain, glossary, business rules |
| [`docs/01-architecture.md`](docs/01-architecture.md) | Stack, render modes, hosting, backups |
| [`docs/02-schema.sql`](docs/02-schema.sql) | Target schema (specification, not a migration) |
| [`docs/03-phases.md`](docs/03-phases.md) | Phase plan with exit criteria |

---

## Local setup

Works identically on macOS and Windows. Rider on both.

### Prerequisites

- .NET SDK — version pinned in `global.json`
- Docker (for a local Postgres, optional — a Neon dev branch also works)
- Tailwind standalone CLI (no Node required)

### First run

```bash
git clone <repo-url> && cd dibal
dotnet restore
```

Set the secrets below, then:

```bash
dotnet ef database update --project src/Dibal.Infrastructure --startup-project src/Dibal.Web
dotnet run --project src/Dibal.Web
```

### Required user-secrets

`dotnet user-secrets` stores these outside the repo. **They do not sync between
machines — that is intentional.** Set them once per machine.

```bash
cd src/Dibal.Web
dotnet user-secrets init

dotnet user-secrets set "ConnectionStrings:Default"   "<neon dev branch pooled connection string>"
dotnet user-secrets set "Storage:AccessKeyId"         "<r2 access key>"
dotnet user-secrets set "Storage:SecretAccessKey"     "<r2 secret>"
dotnet user-secrets set "Storage:Endpoint"            "<r2 endpoint url>"
dotnet user-secrets set "Storage:DocumentsBucket"     "dibal-documents"
dotnet user-secrets set "RootAccount:Email"           "<owner email>"
dotnet user-secrets set "RootAccount:Password"        "<strong password>"
```

Never commit a real connection string. Never put secrets in `appsettings.json`.

---

## Working across two machines

The repo is worked on from a Windows desktop and a Mac. These are the things
that break, and the rules that prevent them.

**Line endings** — handled by `.gitattributes`. Do not override it locally.

**SDK version** — pinned in `global.json`. If a machine errors on startup,
install the pinned SDK rather than editing the pin.

**Case sensitivity** — macOS is case-insensitive; the Linux container is not.
A file referenced as `Client.razor` but named `client.razor` builds fine on the
Mac and fails in the Docker build. Match casing exactly.

**Migrations** — always `git pull` before `dotnet ef migrations add`. Two
migrations created in parallel on different machines produce conflicting parent
IDs and a genuinely painful merge. Never edit a migration that has been applied
anywhere.

**Never build the deployment image locally.** The Mac is ARM64, Fly runs x86-64.
GitHub Actions builds and pushes the image. Local Docker is for running Postgres
during development only.

---

## Deployment

Merging to `main` triggers build → test → format check → **apply migrations** →
deploy to Fly. There is no manual deploy step.

Migrations run *before* the deploy so the new image never serves traffic against
an old schema. That works for additive migrations; a destructive one has to be
split across two deploys. See "Build and deploy ordering" in
[`docs/01-architecture.md`](docs/01-architecture.md).

### Repository secrets

| Secret | Used by |
|---|---|
| `FLY_API_TOKEN` | Deploy |
| `NEON_PROD_CONNECTION` | Migrations, nightly backup |
| `R2_ACCESS_KEY_ID` | Backup upload |
| `R2_SECRET_ACCESS_KEY` | Backup upload |
| `R2_ENDPOINT` | Backup upload |
| `BACKUP_PASSPHRASE` | GPG encryption of the dump |
| `RENDER_DEPLOY_HOOK` | Staging deploy — optional; the job no-ops if unset |

**Store `BACKUP_PASSPHRASE` somewhere outside GitHub as well** — a password
manager. If you lose it, every backup you hold is unreadable.

---

## Restoring from backup

Run this **every quarter**. An untested backup is not a backup.

```bash
# 1. Fetch the dump
aws s3 cp s3://dibal-backups/daily/<file>.dump.gz.gpg . \
  --endpoint-url "$R2_ENDPOINT"

# 2. Verify integrity
sha256sum -c <file>.dump.gz.gpg.sha256

# 3. Decrypt and decompress
gpg --batch --decrypt --passphrase "$BACKUP_PASSPHRASE" \
    <file>.dump.gz.gpg > restore.dump.gz
gunzip restore.dump.gz

# 4. Restore into a SCRATCH Neon branch — never into production
pg_restore --no-owner --no-privileges -d "<scratch branch connection>" restore.dump

# 5. Confirm row counts match production, then delete the scratch branch
```

If any step fails, that is a production incident. Fix it that day.

---

## Costs

| Service | Monthly |
|---|---|
| Fly.io (512MB, Amsterdam) | ~$4 |
| Neon Postgres (free tier) | $0 |
| Cloudflare R2 | $0 |
| GitHub Actions | $0 |
| Render (staging) | $0 |
| UptimeRobot | $0 |

Watch Neon's 0.5 GB storage limit. `audit_log` with jsonb snapshots is what will
consume it — plan to archive rows older than two years to R2 if it gets tight.
