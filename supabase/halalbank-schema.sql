-- Halal Bank Kosova — banking simulation schema (Supabase Postgres)
-- Applied to the Supabase DB. Safe to re-run (idempotent-ish).

create extension if not exists pgcrypto;

-- ---------- tables ----------
create table if not exists public.profiles (
  id         uuid primary key references auth.users(id) on delete cascade,
  full_name  text,
  created_at timestamptz default now()
);

create table if not exists public.accounts (
  id         uuid primary key default gen_random_uuid(),
  user_id    uuid not null references auth.users(id) on delete cascade,
  iban       text unique not null,
  balance    numeric(14,2) not null default 0,
  currency   text not null default 'EUR',
  created_at timestamptz default now()
);

create table if not exists public.transactions (
  id                uuid primary key default gen_random_uuid(),
  account_id        uuid not null references public.accounts(id) on delete cascade,
  amount            numeric(14,2) not null,      -- positive = credit, negative = debit
  kind              text not null,               -- transfer_in | transfer_out | deposit
  counterparty_iban text,
  description       text,
  created_at        timestamptz default now()
);

-- ---------- row level security ----------
alter table public.profiles     enable row level security;
alter table public.accounts     enable row level security;
alter table public.transactions enable row level security;

drop policy if exists "own profile select" on public.profiles;
drop policy if exists "own profile update" on public.profiles;
drop policy if exists "own accounts select" on public.accounts;
drop policy if exists "own tx select" on public.transactions;

create policy "own profile select" on public.profiles for select using (auth.uid() = id);
create policy "own profile update" on public.profiles for update using (auth.uid() = id);
create policy "own accounts select" on public.accounts for select using (auth.uid() = user_id);
create policy "own tx select" on public.transactions for select using (
  exists (select 1 from public.accounts a where a.id = transactions.account_id and a.user_id = auth.uid())
);

-- ---------- helpers ----------
create or replace function public.gen_iban() returns text language sql as $fn$
  select 'XK' || lpad((floor(random()*90+10))::int::text, 2, '0')
              || lpad((floor(random()*100000000000000))::bigint::text, 14, '0')
$fn$;

-- new user -> profile + account seeded with a demo balance
create or replace function public.handle_new_user()
returns trigger language plpgsql security definer set search_path = public as $fn$
begin
  insert into public.profiles(id, full_name)
    values (new.id, coalesce(new.raw_user_meta_data->>'full_name', ''));
  insert into public.accounts(user_id, iban, balance)
    values (new.id, public.gen_iban(), 1000.00);
  return new;
end $fn$;

drop trigger if exists on_auth_user_created on auth.users;
create trigger on_auth_user_created after insert on auth.users
  for each row execute function public.handle_new_user();

-- atomic transfer between accounts by recipient IBAN
create or replace function public.transfer(to_iban text, amt numeric, memo text default '')
returns json language plpgsql security definer set search_path = public as $fn$
declare from_acct public.accounts; to_acct public.accounts;
begin
  if amt is null or amt <= 0 then raise exception 'Amount must be positive'; end if;
  select * into from_acct from public.accounts where user_id = auth.uid() limit 1;
  if not found then raise exception 'No source account'; end if;
  select * into to_acct from public.accounts where iban = to_iban limit 1;
  if not found then raise exception 'Recipient IBAN not found'; end if;
  if to_acct.id = from_acct.id then raise exception 'Cannot transfer to your own account'; end if;
  if from_acct.balance < amt then raise exception 'Insufficient funds'; end if;

  update public.accounts set balance = balance - amt where id = from_acct.id;
  update public.accounts set balance = balance + amt where id = to_acct.id;
  insert into public.transactions(account_id, amount, kind, counterparty_iban, description)
    values (from_acct.id, -amt, 'transfer_out', to_iban, memo);
  insert into public.transactions(account_id, amount, kind, counterparty_iban, description)
    values (to_acct.id,  amt, 'transfer_in', from_acct.iban, memo);
  return json_build_object('ok', true, 'new_balance', (from_acct.balance - amt));
end $fn$;

-- ---------- grants (RLS still restricts rows) ----------
grant usage on schema public to anon, authenticated;
grant select on public.profiles, public.accounts, public.transactions to authenticated;
grant execute on function public.transfer(text, numeric, text) to authenticated;
