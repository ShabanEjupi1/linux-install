-- pos.spacecode.tech — web POS schema (self-hosted Supabase Postgres, Ampere)
-- Multi-tenant rewrite of the KosovaPOS WPF apps:
--   POS  (restaurant/pizzeria)  -> tenant mode 'restaurant'
--   POS2 (retail store, fiscal) -> tenant mode 'store'
-- Field spec comes from POS/Models + POS2/Models (union of both Article/Receipt models).
--
-- Dedicated schema `pos` — must be appended to PGRST_DB_SCHEMAS and selected
-- client-side with `Accept-Profile: pos` / supabase-js `db: { schema: 'pos' }`.
-- Apply as postgres. Idempotent — safe to re-run.  See pos-README.md.

create extension if not exists pgcrypto;

create schema if not exists pos;

-- ---------------------------------------------------------------------------
-- helpers
-- ---------------------------------------------------------------------------

create or replace function pos.set_updated_at()
returns trigger language plpgsql as $$
begin
  new.updated_at := now();
  return new;
end $$;

-- ---------------------------------------------------------------------------
-- tenants & profiles (auth = Supabase auth.users; profiles carries POS role)
-- ---------------------------------------------------------------------------

create table if not exists pos.tenants (
  id             uuid primary key default gen_random_uuid(),
  name           text not null,
  mode           text not null default 'store' check (mode in ('restaurant', 'store')),
  fiscal_number  text,                                  -- Numri Fiskal
  nui            text,                                  -- Numri Unik Identifikues
  vat_number     text,                                  -- Numri i TVSH
  address        text,
  city           text,
  phone          text,
  email          text,
  currency       text not null default 'EUR',
  -- device serial / FLink options / receipt footer etc. live here
  settings       jsonb not null default '{}'::jsonb,
  is_active      boolean not null default true,
  created_at     timestamptz not null default now(),
  updated_at     timestamptz not null default now()
);

create table if not exists pos.profiles (
  id                   uuid primary key references auth.users(id) on delete cascade,
  tenant_id            uuid references pos.tenants(id) on delete set null,
  full_name            text not null default '',
  role                 text not null default 'cashier'
                         check (role in ('admin','manager','cashier','warehouse','accountant')),
  cashier_number       text not null default '01',
  phone                text,
  branch               text,
  -- mirrors the Can* booleans on POS User.cs
  permissions          jsonb not null default '{
    "manage_articles": false, "manage_purchases": false, "manage_users": false,
    "view_reports": true, "modify_prices": false, "delete_receipts": false,
    "give_discounts": false, "manage_delivery": false, "manage_kitchen": false,
    "manage_tables": false, "manage_loyalty": false, "view_analytics": true,
    "manage_inventory": false, "staff_scheduling": false
  }'::jsonb,
  max_discount_percent numeric(5,2) not null default 0,
  is_active            boolean not null default true,
  last_login           timestamptz,
  created_at           timestamptz not null default now(),
  updated_at           timestamptz not null default now()
);

create index if not exists idx_profiles_tenant on pos.profiles (tenant_id);

-- tenant of the calling JWT user (null when no profile yet)
create or replace function pos.current_tenant_id()
returns uuid language sql stable security definer set search_path = pos as $$
  select tenant_id from pos.profiles where id = auth.uid();
$$;

-- permission check against profiles.permissions jsonb; admins pass everything
create or replace function pos.has_permission(perm text)
returns boolean language sql stable security definer set search_path = pos as $$
  select coalesce(
    (select role = 'admin' or coalesce((permissions ->> perm)::boolean, false)
       from pos.profiles where id = auth.uid()),
    false);
$$;

-- ---------------------------------------------------------------------------
-- catalog
-- ---------------------------------------------------------------------------

-- covers KategoriaPos (POS grid categories) and PizzaCategory (menu groups)
create table if not exists pos.categories (
  id            uuid primary key default gen_random_uuid(),
  tenant_id     uuid not null references pos.tenants(id) on delete cascade,
  name          text not null,
  description   text,
  icon          text,
  color         text,
  display_order int not null default 0,
  is_active     boolean not null default true,
  created_at    timestamptz not null default now(),
  updated_at    timestamptz not null default now()
);

create index if not exists idx_categories_tenant on pos.categories (tenant_id, display_order);

-- union of POS/Article.cs and POS2/Article.cs (identical base + pizzeria block)
create table if not exists pos.articles (
  id                     uuid primary key default gen_random_uuid(),
  tenant_id              uuid not null references pos.tenants(id) on delete cascade,
  barcode                text not null default '',
  name                   text not null,
  unit                   text not null default 'Copë',
  sales_unit             text,
  pack                   numeric(12,3) not null default 1,
  purchase_price         numeric(12,2) not null default 0,
  margin                 numeric(12,2) not null default 0,
  package_price          numeric(12,2) not null default 0,
  wholesale_price        numeric(12,2) not null default 0,
  sales_price            numeric(12,2) not null default 0,
  sales_price1           numeric(12,2) not null default 0,
  vat_rate               numeric(5,2)  not null default 18,   -- Kosovo: 18 / 8 / 0
  vat_type               int           not null default 3,    -- 3=18%, 2=8%, 1=0%
  plu                    int,                                 -- fiscal printer PLU
  category_id            uuid references pos.categories(id) on delete set null,
  category               text,                                -- legacy free-text Kategoria
  supplier               text,
  size                   text,
  color                  text,
  brand                  text,
  importer               text,
  location               text,
  sector                 text,
  branch                 text,
  season                 text,
  gender                 text,
  notes                  text,
  stock_quantity         numeric(12,3) not null default 0,
  stock_in               numeric(12,3) not null default 0,
  stock_out              numeric(12,3) not null default 0,
  average_sales_price    numeric(12,2) not null default 0,
  average_purchase_price numeric(12,2) not null default 0,
  minimum_stock          numeric(12,3) not null default 0,
  expiry_date            date,
  has_barcode            boolean not null default true,
  is_regular             boolean not null default true,
  is_weighed             boolean not null default false,
  is_active              boolean not null default true,
  product_type           int not null default 1,              -- 1=Normal, 2=Service
  photo_url              text,
  -- pizzeria block (restaurant mode)
  is_pizza               boolean not null default false,
  pizza_size             text,
  base_price             numeric(12,2) not null default 0,
  pizza_price_small      numeric(12,2),
  pizza_price_medium     numeric(12,2),
  pizza_price_large      numeric(12,2),
  is_customizable        boolean not null default false,
  crust_type             text,
  preparation_time       int not null default 15,             -- minutes
  menu_category          text,
  is_available           boolean not null default true,
  dietary_info           text,
  created_at             timestamptz not null default now(),
  updated_at             timestamptz not null default now()
);

create unique index if not exists uq_articles_tenant_barcode
  on pos.articles (tenant_id, barcode) where barcode <> '';
create unique index if not exists uq_articles_tenant_plu
  on pos.articles (tenant_id, plu) where plu is not null;
create index if not exists idx_articles_tenant_name on pos.articles (tenant_id, name);
create index if not exists idx_articles_tenant_cat  on pos.articles (tenant_id, category_id);

create table if not exists pos.pizza_toppings (
  id            uuid primary key default gen_random_uuid(),
  tenant_id     uuid not null references pos.tenants(id) on delete cascade,
  name          text not null,
  description   text,
  price         numeric(12,2) not null default 0,
  category      text,                                  -- Meat, Vegetable, Cheese, Sauce
  icon          text,
  display_order int not null default 0,
  is_available  boolean not null default true,
  created_at    timestamptz not null default now(),
  updated_at    timestamptz not null default now()
);

create index if not exists idx_toppings_tenant on pos.pizza_toppings (tenant_id, display_order);

-- ---------------------------------------------------------------------------
-- customers / loyalty
-- ---------------------------------------------------------------------------

create table if not exists pos.customers (
  id                    uuid primary key default gen_random_uuid(),
  tenant_id             uuid not null references pos.tenants(id) on delete cascade,
  name                  text not null,
  phone                 text,
  email                 text,
  address               text,
  city                  text,
  loyalty_card_number   text,
  loyalty_points        int not null default 0,
  total_points_earned   int not null default 0,
  total_points_redeemed int not null default 0,
  tier                  text not null default 'Bronze',   -- Bronze/Silver/Gold/Platinum
  total_spent           numeric(12,2) not null default 0,
  store_credit          numeric(12,2) not null default 0,
  order_count           int not null default 0,
  birthday              date,
  preferred_contact     text,
  preferences           jsonb,
  marketing_opt_in      boolean not null default false,
  is_active             boolean not null default true,
  last_visit            timestamptz,
  created_at            timestamptz not null default now(),
  updated_at            timestamptz not null default now()
);

create unique index if not exists uq_customers_tenant_card
  on pos.customers (tenant_id, loyalty_card_number) where loyalty_card_number is not null;
create index if not exists idx_customers_tenant_phone on pos.customers (tenant_id, phone);

create table if not exists pos.loyalty_transactions (
  id               uuid primary key default gen_random_uuid(),
  tenant_id        uuid not null references pos.tenants(id) on delete cascade,
  customer_id      uuid not null references pos.customers(id) on delete cascade,
  points           int not null,                        -- earned > 0, redeemed < 0
  transaction_type text not null default 'Earned'
                     check (transaction_type in ('Earned','Redeemed','Bonus','Expired','Adjusted')),
  reference_number text,
  amount           numeric(12,2) not null default 0,
  description      text,
  created_at       timestamptz not null default now()
);

create index if not exists idx_loyalty_tx_customer on pos.loyalty_transactions (customer_id, created_at desc);

-- ---------------------------------------------------------------------------
-- restaurant floor (restaurant mode)
-- ---------------------------------------------------------------------------

create table if not exists pos.restaurant_tables (
  id                 uuid primary key default gen_random_uuid(),
  tenant_id          uuid not null references pos.tenants(id) on delete cascade,
  table_number       text not null,
  capacity           int not null default 4,
  location           text,                              -- Indoor/Outdoor/VIP
  status             text not null default 'available'
                       check (status in ('available','occupied','reserved','cleaning')),
  current_receipt_id uuid,                              -- fk added after receipts
  occupied_since     timestamptz,
  is_active          boolean not null default true,
  created_at         timestamptz not null default now(),
  updated_at         timestamptz not null default now(),
  unique (tenant_id, table_number)
);

create table if not exists pos.table_reservations (
  id             uuid primary key default gen_random_uuid(),
  tenant_id      uuid not null references pos.tenants(id) on delete cascade,
  table_id       uuid not null references pos.restaurant_tables(id) on delete cascade,
  customer_name  text not null,
  customer_phone text,
  reserved_at    timestamptz not null,
  guest_count    int not null default 1,
  status         text not null default 'pending'
                   check (status in ('pending','confirmed','cancelled','completed')),
  notes          text,
  created_at     timestamptz not null default now(),
  updated_at     timestamptz not null default now()
);

create index if not exists idx_reservations_tenant on pos.table_reservations (tenant_id, reserved_at);

-- ---------------------------------------------------------------------------
-- receipts (union of POS + POS2 Receipt.cs incl. B2B buyer block)
-- ---------------------------------------------------------------------------

-- per-tenant sequential numbering (fiscal requirement) — one row per tenant+year
create table if not exists pos.receipt_counters (
  tenant_id uuid not null references pos.tenants(id) on delete cascade,
  year      int  not null,
  counter   bigint not null default 0,
  primary key (tenant_id, year)
);

create table if not exists pos.receipts (
  id                   uuid primary key default gen_random_uuid(),
  tenant_id            uuid not null references pos.tenants(id) on delete cascade,
  receipt_number       text,                            -- assigned on completion
  status               text not null default 'open'
                         check (status in ('open','completed','void')),
  date                 timestamptz not null default now(),
  buyer_name           text not null default 'Qytetar',
  -- B2B invoice block (POS2)
  buyer_business_name  text,
  buyer_nui            text,
  buyer_fiscal_number  text,
  buyer_address        text,
  buyer_email          text,
  buyer_phone          text,
  remark               text,
  cashier_id           uuid references pos.profiles(id) on delete set null,
  cashier_number       text not null default '01',
  cashier_name         text not null default '',
  total_amount         numeric(12,2) not null default 0,
  paid_amount          numeric(12,2) not null default 0,
  left_amount          numeric(12,2) not null default 0,
  tax_amount           numeric(12,2) not null default 0,
  payment_method       text not null default 'Para në dorë',
  receipt_type         text not null default 'fiscal'
                         check (receipt_type in ('fiscal','simple','waybill')),
  customer_id          uuid references pos.customers(id) on delete set null,
  table_id             uuid references pos.restaurant_tables(id) on delete set null,
  order_type           text not null default 'dine_in'
                         check (order_type in ('dine_in','take_out','delivery')),
  is_fiscal            boolean not null default true,
  is_printed           boolean not null default false,
  created_at           timestamptz not null default now(),
  updated_at           timestamptz not null default now()
);

create unique index if not exists uq_receipts_tenant_number
  on pos.receipts (tenant_id, receipt_number) where receipt_number is not null;
create index if not exists idx_receipts_tenant_date   on pos.receipts (tenant_id, date desc);
create index if not exists idx_receipts_tenant_status on pos.receipts (tenant_id, status);

do $$ begin
  alter table pos.restaurant_tables
    add constraint fk_tables_current_receipt
    foreign key (current_receipt_id) references pos.receipts(id) on delete set null;
exception when duplicate_object then null; end $$;

create table if not exists pos.receipt_items (
  id               uuid primary key default gen_random_uuid(),
  tenant_id        uuid not null references pos.tenants(id) on delete cascade,
  receipt_id       uuid not null references pos.receipts(id) on delete cascade,
  article_id       uuid references pos.articles(id) on delete set null,
  plu              int not null default 0,
  barcode          text not null default '',
  article_name     text not null,
  quantity         numeric(12,3) not null default 1,
  price            numeric(12,2) not null default 0,
  discount_percent numeric(5,2)  not null default 0,
  discount_value   numeric(12,2) not null default 0,
  vat_rate         numeric(5,2)  not null default 18,
  vat_value        numeric(12,2) not null default 0,
  total_value      numeric(12,2) not null default 0,   -- (qty*price) - discount
  created_at       timestamptz not null default now()
);

create index if not exists idx_receipt_items_receipt on pos.receipt_items (receipt_id);

-- PizzaOrderTopping: toppings chosen for one line item
create table if not exists pos.receipt_item_toppings (
  id              uuid primary key default gen_random_uuid(),
  tenant_id       uuid not null references pos.tenants(id) on delete cascade,
  receipt_item_id uuid not null references pos.receipt_items(id) on delete cascade,
  topping_id      uuid references pos.pizza_toppings(id) on delete set null,
  topping_name    text not null default '',
  quantity        numeric(6,2) not null default 1,      -- 0.5 half / 2 double
  price           numeric(12,2) not null default 0,     -- price at time of order
  created_at      timestamptz not null default now()
);

create index if not exists idx_item_toppings_item on pos.receipt_item_toppings (receipt_item_id);

-- ---------------------------------------------------------------------------
-- audit log
-- ---------------------------------------------------------------------------

create table if not exists pos.audit_logs (
  id         uuid primary key default gen_random_uuid(),
  tenant_id  uuid not null references pos.tenants(id) on delete cascade,
  user_id    uuid references pos.profiles(id) on delete set null,
  action     text not null,
  table_name text not null default '',
  record_id  text,
  old_values jsonb,
  new_values jsonb,
  ip_address text,
  created_at timestamptz not null default now()
);

create index if not exists idx_audit_tenant_date on pos.audit_logs (tenant_id, created_at desc);

-- ---------------------------------------------------------------------------
-- updated_at triggers
-- ---------------------------------------------------------------------------

do $$
declare t text;
begin
  foreach t in array array['tenants','profiles','categories','articles','pizza_toppings',
                           'customers','restaurant_tables','table_reservations','receipts']
  loop
    execute format('drop trigger if exists trg_%I_updated_at on pos.%I', t, t);
    execute format('create trigger trg_%I_updated_at before update on pos.%I
                    for each row execute function pos.set_updated_at()', t, t);
  end loop;
end $$;

-- ---------------------------------------------------------------------------
-- workflow functions
-- ---------------------------------------------------------------------------

-- next sequential receipt number for a tenant, e.g. 2026-000042 (row-locked, race-safe)
create or replace function pos.next_receipt_number(p_tenant uuid)
returns text language plpgsql as $$
declare y int := extract(year from now())::int; n bigint;
begin
  insert into pos.receipt_counters (tenant_id, year, counter)
       values (p_tenant, y, 1)
  on conflict (tenant_id, year) do update set counter = pos.receipt_counters.counter + 1
  returning counter into n;
  return format('%s-%s', y, lpad(n::text, 6, '0'));
end $$;

-- finalize a sale: assign number, recompute totals from items, decrement stock,
-- free the table. Runs as caller — RLS scopes everything to their tenant.
create or replace function pos.complete_receipt(p_receipt uuid, p_paid numeric default null)
returns pos.receipts language plpgsql as $$
declare r pos.receipts;
begin
  select * into r from pos.receipts where id = p_receipt for update;
  if not found then raise exception 'Receipt not found'; end if;
  if r.status <> 'open' then raise exception 'Receipt is %, not open', r.status; end if;

  select coalesce(sum(total_value), 0), coalesce(sum(vat_value), 0)
    into r.total_amount, r.tax_amount
    from pos.receipt_items where receipt_id = p_receipt;
  if r.total_amount = 0 then raise exception 'Receipt has no items'; end if;

  -- toppings are part of the bill
  r.total_amount := r.total_amount + coalesce(
    (select sum(t.price * t.quantity) from pos.receipt_item_toppings t
      join pos.receipt_items i on i.id = t.receipt_item_id
     where i.receipt_id = p_receipt), 0);

  update pos.articles a
     set stock_quantity = a.stock_quantity - s.qty,
         stock_out      = a.stock_out + s.qty
    from (select article_id, sum(quantity) qty from pos.receipt_items
           where receipt_id = p_receipt and article_id is not null
           group by article_id) s
   where a.id = s.article_id;

  update pos.receipts
     set status         = 'completed',
         receipt_number = coalesce(receipt_number, pos.next_receipt_number(tenant_id)),
         total_amount   = r.total_amount,
         tax_amount     = r.tax_amount,
         paid_amount    = coalesce(p_paid, r.total_amount),
         left_amount    = greatest(r.total_amount - coalesce(p_paid, r.total_amount), 0),
         date           = now()
   where id = p_receipt
   returning * into r;

  update pos.restaurant_tables
     set status = 'available', current_receipt_id = null, occupied_since = null
   where current_receipt_id = p_receipt;

  return r;
end $$;

-- create a tenant + promote an existing auth user to its admin.
-- service_role only (operator provisions businesses; no self-service signup).
create or replace function pos.bootstrap_tenant(
  p_name text, p_mode text, p_admin_user uuid,
  p_admin_name text default '', p_fiscal_number text default null)
returns uuid language plpgsql security definer set search_path = pos as $$
declare tid uuid;
begin
  insert into pos.tenants (name, mode, fiscal_number)
       values (p_name, p_mode, p_fiscal_number) returning id into tid;
  insert into pos.profiles (id, tenant_id, full_name, role, permissions)
       values (p_admin_user, tid, p_admin_name, 'admin', '{}'::jsonb)
  on conflict (id) do update
       set tenant_id = tid, role = 'admin', full_name = excluded.full_name;
  return tid;
end $$;

revoke all on function pos.bootstrap_tenant(text,text,uuid,text,text) from public, anon, authenticated;

-- attach an auth user to the caller's tenant (admins only, or service_role)
create or replace function pos.add_staff(
  p_user uuid, p_full_name text, p_role text default 'cashier')
returns void language plpgsql security definer set search_path = pos as $$
declare tid uuid := pos.current_tenant_id();
begin
  if tid is null or not pos.has_permission('manage_users') then
    raise exception 'Not authorized to manage users';
  end if;
  insert into pos.profiles (id, tenant_id, full_name, role)
       values (p_user, tid, p_full_name, p_role)
  on conflict (id) do update
       set tenant_id = tid, full_name = excluded.full_name, role = excluded.role;
end $$;

-- ---------------------------------------------------------------------------
-- row level security — everything scoped to the caller's tenant
-- ---------------------------------------------------------------------------

do $$
declare t text;
begin
  foreach t in array array['tenants','profiles','categories','articles','pizza_toppings',
                           'customers','loyalty_transactions','restaurant_tables',
                           'table_reservations','receipt_counters','receipts',
                           'receipt_items','receipt_item_toppings','audit_logs']
  loop
    execute format('alter table pos.%I enable row level security', t);
  end loop;

  -- one read + full-write policy pair per tenant-scoped table
  foreach t in array array['categories','articles','pizza_toppings','customers',
                           'loyalty_transactions','restaurant_tables','table_reservations',
                           'receipt_counters','receipts','receipt_items',
                           'receipt_item_toppings','audit_logs']
  loop
    execute format('drop policy if exists tenant_select on pos.%I', t);
    execute format('create policy tenant_select on pos.%I for select to authenticated
                    using (tenant_id = pos.current_tenant_id())', t);
    execute format('drop policy if exists tenant_insert on pos.%I', t);
    execute format('create policy tenant_insert on pos.%I for insert to authenticated
                    with check (tenant_id = pos.current_tenant_id())', t);
    execute format('drop policy if exists tenant_update on pos.%I', t);
    execute format('create policy tenant_update on pos.%I for update to authenticated
                    using (tenant_id = pos.current_tenant_id())
                    with check (tenant_id = pos.current_tenant_id())', t);
  end loop;
end $$;

-- deletes are restricted: receipts need the delete_receipts permission,
-- catalog/floor deletes need manage_articles; line items follow their receipt
drop policy if exists tenant_delete on pos.receipts;
create policy tenant_delete on pos.receipts for delete to authenticated
  using (tenant_id = pos.current_tenant_id()
         and (status = 'open' or pos.has_permission('delete_receipts')));

do $$
declare t text;
begin
  foreach t in array array['receipt_items','receipt_item_toppings'] loop
    execute format('drop policy if exists tenant_delete on pos.%I', t);
    execute format('create policy tenant_delete on pos.%I for delete to authenticated
                    using (tenant_id = pos.current_tenant_id())', t);
  end loop;
  foreach t in array array['categories','articles','pizza_toppings','customers',
                           'restaurant_tables','table_reservations'] loop
    execute format('drop policy if exists tenant_delete on pos.%I', t);
    execute format('create policy tenant_delete on pos.%I for delete to authenticated
                    using (tenant_id = pos.current_tenant_id()
                           and pos.has_permission(''manage_articles''))', t);
  end loop;
end $$;

-- tenants: members read their own row; only admins update it
drop policy if exists tenant_self_select on pos.tenants;
create policy tenant_self_select on pos.tenants for select to authenticated
  using (id = pos.current_tenant_id());
drop policy if exists tenant_self_update on pos.tenants;
create policy tenant_self_update on pos.tenants for update to authenticated
  using (id = pos.current_tenant_id() and pos.has_permission('manage_users'))
  with check (id = pos.current_tenant_id());

-- profiles: see colleagues; edit only yourself unless you manage users
drop policy if exists profiles_select on pos.profiles;
create policy profiles_select on pos.profiles for select to authenticated
  using (id = auth.uid() or tenant_id = pos.current_tenant_id());
drop policy if exists profiles_update on pos.profiles;
create policy profiles_update on pos.profiles for update to authenticated
  using (id = auth.uid()
         or (tenant_id = pos.current_tenant_id() and pos.has_permission('manage_users')))
  with check (tenant_id = pos.current_tenant_id());

-- ---------------------------------------------------------------------------
-- grants (PostgREST roles; service_role bypasses RLS)
-- ---------------------------------------------------------------------------

grant usage on schema pos to authenticated, service_role;
grant select, insert, update, delete on all tables in schema pos to authenticated, service_role;
grant execute on all functions in schema pos to authenticated, service_role;
revoke all on function pos.bootstrap_tenant(text,text,uuid,text,text) from authenticated;
alter default privileges in schema pos
  grant select, insert, update, delete on tables to authenticated, service_role;

-- live table/receipt updates for the restaurant floor UI
do $$ begin
  alter publication supabase_realtime add table pos.receipts;
exception when duplicate_object then null; end $$;
do $$ begin
  alter publication supabase_realtime add table pos.restaurant_tables;
exception when duplicate_object then null; end $$;
