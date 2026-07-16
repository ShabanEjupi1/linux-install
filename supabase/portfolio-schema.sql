-- shabanejupi.tech portfolio — public-readable projects
create table if not exists public.projects (
  id         uuid primary key default gen_random_uuid(),
  title      text not null,
  blurb      text,
  url        text,
  tags       text[] default '{}',
  sort       int default 0,
  created_at timestamptz default now()
);

alter table public.projects enable row level security;
drop policy if exists "public read projects" on public.projects;
create policy "public read projects" on public.projects for select using (true);
grant select on public.projects to anon, authenticated;

-- seed a few real projects (only if table is empty)
insert into public.projects (title, blurb, url, tags, sort)
select * from (values
  ('Self-hosted cloud platform',
   'A full stack running on bare metal + Oracle Cloud: Docker, Cloudflare Tunnels, Supabase, OpenSearch, Meilisearch and analytics — no vendor lock-in.',
   'https://shabanejupi.tech', array['docker','cloudflare','supabase'], 1),
  ('Halal Bank Kosova',
   'A Sharia-compliant banking app: real signup/login, IBAN accounts, and atomic instant transfers, secured by Postgres row-level security.',
   'https://halalbankkosova.me', array['astro','supabase','auth'], 2),
  ('Jitsi Meet server',
   'Self-hosted, encrypted video conferencing with automatic Let''s Encrypt TLS, running on an Oracle Cloud VM.',
   'https://meet.shabanejupi.tech', array['jitsi','webrtc','oracle'], 3)
) as v(title, blurb, url, tags, sort)
where not exists (select 1 from public.projects);
