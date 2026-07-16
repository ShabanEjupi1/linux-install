# pos-web — KosovaPOS në web

Web rewrite of the two WPF apps (`POS/` restaurant, `POS2/` store) on the shared
self-hosted Supabase (schema `pos`, see [../supabase/pos-schema.sql](../supabase/pos-schema.sql)).
One SPA, tenant `mode` decides the UI (restaurant: order types, tables, pizza sizes;
store: barcode-first, PLU, B2B invoice block).

- Live: https://pos.spacecode.tech (Ampere `~/apps/pos-web`, nginx :8120)
- Login: Supabase auth; users are attached to a tenant via `pos.profiles`
  (provision with `pos.bootstrap_tenant(...)` / `rpc add_staff`).

## Build & deploy

```bash
npm install && npm run build
scp -r dist deploy/* ampere:~/apps/pos-web/   # then chmod 755 dirs / 644 files!
ssh ampere 'cd ~/apps/pos-web && sudo docker compose up -d'
```

## Current scope (session 2)

Sale screen (categories, search/barcode, cart, pizza sizes, payment + change,
B2B buyer block), receipt completion via `rpc complete_receipt` (sequential
fiscal numbering + stock decrement), receipts list per day with 80mm-style
print/PDF, article + category management.

Deferred: toppings picker, purchases/inventory, cash shifts / Z-reports,
kitchen/delivery, loyalty UI, fiscal printer agent (FLink bridge on shop PC).
