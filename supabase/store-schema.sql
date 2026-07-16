-- enisi.tech — online store schema (Supabase Postgres)
create extension if not exists pgcrypto;

create table if not exists public.categories (
  id serial primary key, slug text unique not null, name text not null, sort int default 0
);
create table if not exists public.products (
  id uuid primary key default gen_random_uuid(),
  name text not null, slug text unique, description text,
  price numeric(10,2) not null, category_id int references public.categories(id),
  image_url text, stock int default 100, featured boolean default false,
  created_at timestamptz default now()
);
create table if not exists public.orders (
  id uuid primary key default gen_random_uuid(),
  customer_name text, email text, address text, phone text,
  total numeric(10,2) default 0, status text default 'pending', created_at timestamptz default now()
);
create table if not exists public.order_items (
  id uuid primary key default gen_random_uuid(),
  order_id uuid references public.orders(id) on delete cascade,
  product_id uuid references public.products(id), name text, price numeric(10,2), qty int
);

alter table public.categories  enable row level security;
alter table public.products    enable row level security;
alter table public.orders      enable row level security;
alter table public.order_items enable row level security;

drop policy if exists "read cats"  on public.categories;
drop policy if exists "read prods" on public.products;
create policy "read cats"  on public.categories for select using (true);
create policy "read prods" on public.products   for select using (true);

grant select on public.categories, public.products to anon, authenticated;
grant usage, select on sequence public.categories_id_seq to anon, authenticated;

-- atomic order placement: prices come from the DB, not the client
create or replace function public.place_order(p_name text, p_email text, p_address text, p_phone text, p_items jsonb)
returns uuid language plpgsql security definer set search_path = public as $fn$
declare oid uuid; item jsonb; prod public.products; tot numeric(10,2) := 0; q int;
begin
  if p_name is null or p_email is null then raise exception 'Name and email required'; end if;
  insert into public.orders(customer_name,email,address,phone,total,status)
    values (p_name,p_email,p_address,p_phone,0,'pending') returning id into oid;
  for item in select * from jsonb_array_elements(p_items) loop
    select * into prod from public.products where id = (item->>'id')::uuid;
    if found then
      q := greatest(1, coalesce((item->>'qty')::int, 1));
      insert into public.order_items(order_id,product_id,name,price,qty)
        values (oid, prod.id, prod.name, prod.price, q);
      tot := tot + prod.price * q;
    end if;
  end loop;
  if tot = 0 then delete from public.orders where id = oid; raise exception 'Empty cart'; end if;
  update public.orders set total = tot where id = oid;
  return oid;
end $fn$;
grant execute on function public.place_order(text,text,text,text,jsonb) to anon, authenticated;

-- ---------- seed ----------
insert into public.categories (slug, name, sort) values
  ('clothes','Children''s Clothes',1), ('shoes','Shoes',2), ('cosmetics','Cosmetics',3)
on conflict (slug) do nothing;

insert into public.products (name, slug, description, price, category_id, image_url, featured)
select v.name, v.slug, v.descr, v.price, c.id, v.img, v.feat from (values
  ('Cotton Baby Onesie','baby-onesie','Soft 100% cotton onesie, 0–12 months.',12.90,'clothes','https://picsum.photos/seed/onesie/500/500',true),
  ('Kids Hooded Sweatshirt','kids-hoodie','Warm fleece hoodie for ages 2–8.',19.50,'clothes','https://picsum.photos/seed/hoodie/500/500',true),
  ('Girls Summer Dress','girls-dress','Light floral dress, breathable fabric.',22.00,'clothes','https://picsum.photos/seed/dress/500/500',false),
  ('Boys Denim Jeans','boys-jeans','Durable adjustable-waist jeans.',18.00,'clothes','https://picsum.photos/seed/jeans/500/500',false),
  ('Toddler Sneakers','toddler-sneakers','Velcro sneakers with soft soles.',24.90,'shoes','https://picsum.photos/seed/sneakers/500/500',true),
  ('Kids Rain Boots','rain-boots','Waterproof boots for rainy days.',16.50,'shoes','https://picsum.photos/seed/rainboots/500/500',false),
  ('School Shoes','school-shoes','Classic black school shoes.',27.00,'shoes','https://picsum.photos/seed/schoolshoes/500/500',false),
  ('Baby Shampoo','baby-shampoo','Tear-free gentle shampoo, 250ml.',6.90,'cosmetics','https://picsum.photos/seed/shampoo/500/500',true),
  ('Moisturizing Baby Lotion','baby-lotion','Hypoallergenic daily lotion.',8.50,'cosmetics','https://picsum.photos/seed/lotion/500/500',false),
  ('Diaper Rash Cream','rash-cream','Protective zinc cream, 100ml.',7.20,'cosmetics','https://picsum.photos/seed/cream/500/500',false),
  ('Kids Toothpaste Set','toothpaste-set','Fluoride-free, fruit flavor, 2-pack.',5.50,'cosmetics','https://picsum.photos/seed/toothpaste/500/500',false),
  ('Sunscreen SPF50 Kids','kids-sunscreen','Mineral sunscreen for sensitive skin.',11.00,'cosmetics','https://picsum.photos/seed/sunscreen/500/500',true)
) as v(name,slug,descr,price,cat,img,feat)
join public.categories c on c.slug = v.cat
on conflict (slug) do nothing;
