# 00 — Overview

## What the system does

Internal back-office system for a weighing-scale distributor. Replaces
spreadsheets for tracking who bought which physical unit, when the warranty
runs out, and what is left in stock.

Five capabilities:

1. **Client directory** — business customers, searchable by business name,
   client name, or postcode
2. **Stock** — serialised units, goods-in from the supplier, in/low/out-of-stock
   visibility
3. **Deliveries** — which specific serial numbers went to which client, when
4. **Documents** — delivery/warranty notes as PDF, stored permanently,
   emailable to the client
5. **Support tracking** — warranty and support periods per unit, with renewals

## Users

3–5 internal staff. One Owner (root) account, plus Manager and Staff roles.
No customer-facing access.

Primarily desktop. Four screens must work properly on a phone: serial lookup,
stock levels, client detail, delivery detail (with resend-document). Heavy data
entry screens are desktop-only by design and show a "use a desktop" panel below
the `md` breakpoint.

## Domain glossary

| Term | Meaning |
|---|---|
| **Scale model** | A catalogue entry, e.g. "Model X, 15–30kg". Not a physical object. |
| **Stock unit** | One physical scale, identified by serial number. |
| **Serial number** | Unique **per model**, not globally. No enforced format. |
| **Size band** | `6–15kg` or `15–30kg`. A property of the model. |
| **Goods-in batch** | A shipment received from the supplier (always Dibal). |
| **Delivery** | A record of units going to a client. Was called "sales record". |
| **Delivery line** | One serial number on a delivery, with its own warranty/support dates. |
| **Customer type** | `Reseller` or `EndUser`. |
| **Warranty end** | Date the manufacturer warranty expires. Per unit. |
| **Support period** | Start/end dates for the support contract. Per unit. Renewable. |
| **Document** | A generated PDF delivery/warranty note. Immutable once created. |

## Key domain rules

- **A serial number is unique within a model**, not across all models. Serial
  lookup can therefore return multiple results and must show a result list.
- **Warranty and support status are derived from dates**, never stored.
- **Stock deducts on delivery confirmation**, not on draft creation.
- **Cancelling a confirmed delivery returns its units to stock** via
  compensating movements. It does not delete the delivery.
- **Support renewals extend the existing delivery line** and log the previous
  end date in `support_renewals`. History is never overwritten.
- **Low stock threshold is 5 by default**, global, with optional per-model
  override.
- **The only supplier is Dibal.** `supplier_name` exists and defaults to
  `'Dibal'` but there is no suppliers table.

## What is deliberately not in v1

Returns/RMA, prices and invoicing, VAT, payments, multi-warehouse,
customer portal. See `CLAUDE.md` for why the schema still reserves space for
some of these.

## Naming note

The solution is named `Dibal` (`src/Dibal.Domain`, `src/Dibal.Web`, …), matching
the repository. This resolves the earlier `ScaleOps` placeholder.

**"Dibal" now means two things, and only one of them is the software.** The
supplier is also Dibal — it is the only supplier, per the domain rules above.
So `goods_in_batches.supplier_name text NOT NULL DEFAULT 'Dibal'` in
`02-schema.sql` is **supplier data, not a stray project name**. Leave it alone.
If a second supplier is ever added, that column starts varying; the solution
name does not.
