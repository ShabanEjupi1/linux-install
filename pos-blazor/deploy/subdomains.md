# Per-business subdomains — `pos-<code>.spacecode.tech`

Every business is reachable at `pos-<code>.spacecode.tech` (e.g. `pos-bmd.spacecode.tech`).
The apex `pos.spacecode.tech` keeps the code-field login and hosts the platform admin
console at `/admin`.

The whole point of the design: **one DNS record and one tunnel rule serve every present
and future shop.** Creating a business in `/admin/bizneset` makes its URL work immediately,
with zero per-shop DNS or tunnel changes.

## Why first-level with a `pos-` prefix

- **Cert is free.** Cloudflare's Universal SSL covers the apex and *one* level of subdomain,
  so `pos-bmd.spacecode.tech` is inside the `*.spacecode.tech` wildcard cert at no cost. A
  two-level name (`bmd.pos.spacecode.tech`) would need paid Advanced Certificate Manager.
- **No namespace collisions.** The `pos-` prefix keeps shop hostnames away from infra
  subdomains (`mail`, `git`, `ssh`, `audit`, …). A business code can never shadow one.
- **cloudflared can't express `pos-*` anyway.** It only treats a rule as a wildcard when it
  starts with `*.` (`strings.HasPrefix(ruleHost, "*.")`). So the edge rule is `*.spacecode.tech`,
  placed *after* the specific infra rules, and the `pos-` scoping lives in the app
  (`BusinessHostResolver`), which ignores any host that isn't `pos-<valid-code>`.

## One-time setup

### 1. DNS — a single proxied wildcard

In the Cloudflare dashboard for `spacecode.tech`, add:

```
Type: CNAME   Name: *   Target: <TUNNEL-UUID>.cfargotunnel.com   Proxy: ON (orange cloud)
```

Specific records (`mail`, `git`, `ssh`, `pos`, …) take precedence over the wildcard, so they
are unaffected. Any `pos-<code>` name falls through to the wildcard → tunnel.

> Trade-off: this makes the POS the catch-all for *any* otherwise-unmatched first-level
> subdomain on the zone. That is fine — the app answers only hosts it recognises
> (`pos-<code>` with an active business) and shows the apex login for anything else. If you
> want tighter scoping instead, drop the wildcard and add one `pos-<code>` CNAME per shop
> (via `cloudflared tunnel route dns <tunnel> pos-<code>.spacecode.tech`) at onboarding.

### 2. Tunnel ingress — one wildcard rule, after the specifics

In the live cloudflared config (the committed `cloudflare/tunnel-spacecode.yml` is a sample —
apply the same rule to whatever tunnel is live on Ampere). Order matters: cloudflared matches
top-to-bottom, first match wins, so infra hostnames must come *before* the wildcard, and the
`404` catch-all must stay last.

```yaml
ingress:
  # ── specific infra hostnames first ──
  - hostname: audit.spacecode.tech
    service: http://localhost:8002
  - hostname: ssh.spacecode.tech
    service: ssh://localhost:22
  # ── every POS host (apex + pos-<code>) → nginx → the Blazor app ──
  - hostname: pos.spacecode.tech
    service: http://localhost:80
  - hostname: "*.spacecode.tech"
    service: http://localhost:80
  # ── must be last ──
  - service: http_status:404
```

(`service` points at nginx on :80, which regex-matches `pos`/`pos-<code>` and proxies to the
app on :8121. If you route the tunnel straight at the app instead, use
`http://localhost:8121` and drop nginx — but then re-add the WebSocket upgrade handling the
app needs for `/_blazor`.)

### 3. Cert

`*.spacecode.tech` Universal SSL already covers every `pos-<code>.spacecode.tech`. Nothing to buy.

## App configuration

Set on the `pos-blazor` container (see `docker-compose.yml`):

- `POS_BASE_HOST=spacecode.tech` — the zone apex.
- `POS_HOST_PREFIX=pos-` — the label prefix. Set empty to drop it (then codes share the apex
  namespace and you must avoid infra collisions yourself).

## How it behaves

- `pos-bmd.spacecode.tech/login` → business is fixed by the host, the code field is hidden,
  the card shows the shop's name. Login needs only username + password.
- `pos.spacecode.tech/login` → the code field is shown (apex fallback; also where the primary
  business logs in until it uses its own subdomain).
- The auth cookie is **host-only** (no `Domain` is set), so a session minted on `pos-bmd`
  is never even sent to `pos-enisi`. Defence in depth: a middleware also refuses to serve a
  session on any business host other than its own and bounces it to that host's `/login`.
