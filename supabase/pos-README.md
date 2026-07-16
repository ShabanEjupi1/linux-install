# Web POS — `pos` schema on self-hosted Supabase (Ampere)

Multi-tenant Postgres schema for the web rewrite of the two KosovaPOS WPF apps:
`POS/` (restaurant → tenant mode `restaurant`) and `POS2/` (retail store → mode
`store`). One platform, one schema, tenant rows keep the data apart via RLS.

Schema file: [pos-schema.sql](pos-schema.sql) — idempotent, safe to re-run.
Validated on Postgres 15: double-apply, RLS tenant isolation, `complete_receipt`
flow (sequential numbering, stock decrement, permission-gated deletes).

## What's inside

| Area | Tables |
|---|---|
| Tenancy & auth | `tenants`, `profiles` (1:1 with `auth.users`, carries role + permission jsonb) |
| Catalog | `categories`, `articles` (union of both apps' Article models incl. pizza block, PLU, VAT 18/8/0), `pizza_toppings` |
| Loyalty | `customers`, `loyalty_transactions` |
| Restaurant floor | `restaurant_tables`, `table_reservations` |
| Sales | `receipts` (incl. POS2 B2B buyer/NUI block), `receipt_items`, `receipt_item_toppings`, `receipt_counters` |
| Ops | `audit_logs` |

Functions: `complete_receipt(receipt, paid)` finalizes a sale atomically
(totals from items, per-tenant sequential `YYYY-NNNNNN` number, stock decrement,
table release); `bootstrap_tenant(...)` (service-role only) provisions a
business + its admin; `add_staff(...)` lets a tenant admin attach users.

## Apply on Ampere

```bash
scp supabase/pos-schema.sql ampere:/tmp/
ssh ampere 'docker exec -i supabase-db psql -U postgres -d postgres < /tmp/pos-schema.sql'
```

## Expose to PostgREST

Append `pos` to `PGRST_DB_SCHEMAS` in the Supabase docker `.env`
(comma-separated list — keep the existing entries like `spacerent`), then:

```bash
ssh ampere 'cd <supabase-docker>/docker && docker compose up -d rest'
```

## Client wiring

```js
const supabase = createClient(SUPABASE_URL, ANON_KEY, { db: { schema: 'pos' } })
// login/logout = supabase.auth.signInWithPassword / signOut (unchanged)
```

## Provision the first tenant

1. Create the admin user in Supabase (Dashboard → Auth, or admin API) and copy its UUID.
2. As postgres:

```sql
select pos.bootstrap_tenant('Pizzeria Enisi', 'restaurant',
       '<auth-user-uuid>', 'Shaban Ejupi', '<fiscal-number>');
```

3. Further staff: create the auth user, then the tenant admin calls
   `rpc('add_staff', { p_user, p_full_name, p_role })`.

## Deliberately deferred (next migrations)

- Purchases/inventory movements, suppliers (session 3)
- Store mode: cash shifts, Z-reports, gift cards, price rules, DBF import (session 4)
- Kitchen orders + delivery module (restaurant session 3)
- Fiscal printer: browsers can't reach it — start with PDF receipts; a small
  local agent on the shop PC can bridge to FLink/Datecs later.
- Article photos: create a `pos-photos` storage bucket; `articles.photo_url` holds the URL.
