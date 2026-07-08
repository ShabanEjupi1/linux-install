# KosovaPOS — WPF → ASP.NET Core (Blazor Server) migration

Converting the two WPF desktop apps (`../POS` restaurant, `../POS2` store) into
**one shared Blazor Server web app**, both tenants, on **EF Core + Postgres (Npgsql)**.

Decisions (2026-07-08):
- **Scope:** single shared app, tenant mode switches UI (same idea as `../pos-web`).
- **UI stack:** Blazor Server — stay 100% in C#, XAML+code-behind maps to `.razor` + code.
- **Database:** reuse the desktop EF Core models, swap provider to Npgsql/Postgres.

## Why this beats the React rewrite (`../pos-web`)
`../pos-web` is a thin ~950-line React+Supabase SPA that reuses **none** of the C#
business logic and defers most features. This migration keeps the entire
`Services/` + `Models/` + `Database/` investment (payroll, ATK, fiscal, pricing, …)
and only rebuilds the XAML UI layer.

## Solution layout
```
pos-blazor/
  KosovaPOS.sln
  src/
    KosovaPOS.Core/     net10 class lib — shared with the desktop apps
      Models/           LINKED from ../../POS2/Models (single source of truth)
      Data/PosDbContext.cs   web EF context (Npgsql), ported from POS2 POSDbContext
      Services/         AuthService, TenantService  (business logic, no Windows deps)
      Migrations/       EF migrations for the Postgres schema
    KosovaPOS.Web/      net10 Blazor Server front-end
      Components/Pages/ Login.razor, Home.razor (shell)
      Services/UserSession.cs
      Program.cs        DI: DbContextFactory(Npgsql) + AuthService + TenantService
```

## Status — Phase 0–2 done ✅
- Solution scaffolded, builds green on **.NET 10**.
- **38 Models** link into Core and compile unchanged (zero Windows deps).
- `PosDbContext` (~60 entities) → EF migration → **68 tables** created in Postgres.
- **Login works end-to-end** with **cookie auth** (survives reloads): Blazor →
  `AuthService` (ported from `LoginWindow`, BCrypt) → EF/Npgsql. Verified via curl:
  login sets cookie, reload keeps session, logout clears it, protected pages 302 to
  `/login`, wrong pw / unknown user denied.
- Tenant mode = `BusinessSettings.Profile` + `Enable*` flags (reused, not reinvented).
- **Deploy artifacts** ready: `Dockerfile`, `docker-compose.yml`, `deploy/nginx-pos-blazor.conf`.
  `dotnet publish` produces a runnable image input.
- **Sale screen** (`/sale`) done: catalogue grid + category filter + search/barcode-scan,
  cart with qty +/−, live VAT/subtotal/total, payment method + change, complete →
  posts to BMD `DitariD` journal + decrements `Artikujt` stock (ported `SalesService`
  + `CatalogService`). Verified end-to-end on Postgres: journal rows, stock, today-summary.
- **Articles + Receipts** (`/articles`, `/faturat`) done: Articles is a searchable,
  category-filtered table with a modal add/edit form (VAT-type select, live stock) and
  a delete-confirm modal; Receipts lists recent journals (newest first) with a click-to-
  open line-item detail modal. Ported `CatalogService.SaveArticle/Delete/FindById/Count`
  (desktop's raw-SQL stock write dropped — no BMD trigger on Postgres) and
  `SalesService.GetRecentSales/GetReceiptDetail` (group-by aggregation moved to memory —
  Npgsql can't translate the desktop `g.First()` projection). Home tiles now link through.
  Verified: service harness PASS (insert 5→6, round-trip, edit price/stock, categories,
  delete 6→5, recent sales, detail summing 6.50€) + SSR render of both pages with live data.
- **Purchases** (`/blerjet`) done — the mirror of the Sale screen: catalogue picker on
  the left, editable line table (qty / purchase price / sales price / VAT) + supplier
  dropdown + invoice no. + type + paid flag on the right; a header toggle flips to a
  recent-purchases list with a line-item detail modal. New `PurchaseService` (ported from
  `PurchaseEditWindow.xaml.cs`) posts to the BMD `DitariH` journal and **increments**
  `Artikujt` stock while refreshing purchase/sales price, in a transaction. Verified E2E:
  stock 200→210 / 30→35, prices refreshed, journal total 18.48€ (18% + 8% VAT), recent
  list + detail correct; page SSR-renders the entry form with live catalogue.
- **Stock** (`/stoku`) done: read-only valuation table over `Artikujt` (reuses
  `CatalogService`) — search + all/low/out filter + adjustable low-stock threshold, per-row
  cost/retail value, and summary tiles (total cost value, retail value, low count, out count).
- **Reports** (`/raportet`) done via new `ReportService` (ported from `ZReportService`
  + `SalesDataService.GetSalesForDateRange`, read-only — no ZReport persistence / fiscal /
  ATK export): a **daily Z-style report** (sales with/without VAT, 18/8/0 VAT split by the
  vat/net-ratio heuristic, cash vs card, transaction count) and a **date-range summary**
  (sales + purchases totals, per-day breakdown, top-10 articles by revenue). Verified E2E:
  daily 6.50€ = 5.33 net + 1.17 VAT@18%, cross-checks `GetTodaySummary`; range shows
  sales 6.50 + purchases 18.48 + top articles; stock 280€ cost / 608€ retail. Both SSR-render.
- **Gotcha fixed:** Npgsql rejects `Kind=Local` on `timestamptz`. Set
  `Npgsql.EnableLegacyTimestampBehavior` (in `PosDbContext` static ctor) → DateTime maps
  to `timestamp without time zone`, so `DateTime.Now/Today` in ported code works unchanged.
  All 88 datetime columns are now `timestamp without time zone`.

## Roadmap
| Phase | Goal | State |
|-------|------|-------|
| 0 Scaffold | Core + Web, net10, Npgsql | ✅ |
| 1 Core port | Models + DbContext on Postgres | ✅ |
| 2 First slice | Login + shell, tenant-aware, **cookie auth** | ✅ |
| 3 Sale screen | cart, categories/search, barcode, payment/change, receipt→DitariD + stock | ✅ |
| 4 Management | Articles CRUD, Receipts, Purchases, Stock, Reports — all done | ✅ |
| 5 Hardware agent | fiscal/receipt/barcode printers, scale via a local PC agent (browser → agent bridge) | ✅ |
| 6 Deploy | Docker on Ampere, self-provisioning DB, **LIVE at pos.spacecode.tech** | ✅ |
| 7 Core-parity screens | Users, Business Partners, Cash Register/Shifts, Returns, Finance | ✅ |
| 8 Real data | load BMDData `script.sql` (2,184 real articles + history) into Postgres | ✅ |

## Phase 7 — core-parity management screens ✅ (2026-07-08)
Closes the biggest gap vs the WPF store app (POS2 has ~60 windows; the web app had 6).
Added five screens, each a Core service + a `.razor` page mirroring the Articles pattern
(searchable table + modal editor + confirm dialogs), wired into Home tiles:
- **Users** (`/perdoruesit`) — CRUD over the **POSUsers** table (the one login uses),
  BCrypt hashing in `UserService`, role presets (Admin/Manager/Cashier/Warehouse/Accountant)
  auto-fill the permission grid, soft-delete (deactivate) that refuses the last active Admin.
  Tile is gated on the `perm=users` claim.
- **Business Partners** (`/partneret`) — customers + suppliers + both, NUI/NRF, balances;
  `PartnerService`.
- **Cash Register / Shift** (`/arka`) — open shift (float) → live cash/card/expected totals
  from DitariD → close with counted cash + auto-computed difference; `ShiftService` over
  `CashShifts`, one open shift at a time, plus a recent-shifts history.
- **Returns** (`/kthimet`) — look up an original receipt by journal number, pick lines/qty,
  choose refund method, optional restock; `ReturnService` writes `ReturnReceipts`(+items)
  and increments `Artikujt` stock in a transaction. Generates `RET-yyyyMMdd-NNN` numbers.
- **Finance** (`/financat`) — cash-book (ArkaHyrje/ArkaDalje) with income/expense entry +
  date-range totals, and a debts (Borxhi) tab with payment recording; `FinanceService`.

DI registered in `Program.cs`; new CSS (`.panel/.tabs/.tab/.perm-set/.section-title/…`)
appended to `wwwroot/app.css`. **Verified E2E** on a throwaway local Postgres: build green,
login `admin/admin`, all five pages 200 with their DB read-queries executing (no runtime
exceptions in the EF log beyond the expected fresh-DB migration-history probe).
**DEPLOYED to pos.spacecode.tech** (publish → rsync → `docker compose up -d --build pos-blazor`);
all five routes verified 200 over the public URL.

## Phase 8b — remaining tables + Finance wired to the real ledger ✅ (2026-07-08)
Loaded the last high-value tables into the live DB via the same schema-driven ETL
(`scratchpad/etl.py`): **ArkaHyrjeDalje 4,335** cash-ledger rows, **Qytetet 18** cities,
**Punetoret 3** employees. New wrinkle handled: `CAST(0x… AS Date)` **4-byte** binary dates
(int32 days since 0001-01-01, little-endian) — verified against decoded samples (e.g. one
`1924-10-22` outlier is a faithful decode of a source data-entry typo, not a bug). Identity
sequences `setval`'d after load. **Finance fix:** the Phase-7 `FinanceService` cash-book read
the empty `ArkaHyrje`/`ArkaDalje` tables, but the real history lives in **`ArkaHyrjeDalje`**
(the desktop `FinanceWindow`'s main table — VleraH=income / VleraD=expense). Repointed
`GetCashBookAsync`/totals + `Add*` there and defaulted the date range to year-to-date (matching
the desktop). Verified live over pos.spacecode.tech: `/financat` YTD shows **39 entries,
Hyrje 920.90 € / Dalje 0.00 / Neto 920.90 €** with real receipt numbers (`01-KO3986…`).

## Phase 8 — real BMDData load ✅ (2026-07-08)
**DONE and LIVE.** Loaded the real store data into the live `pos-blazor-db`: **2,184 articles**
(Artikujt), 95 suppliers (FurnitoriNew), 1 category, 2,211 purchase-journal rows (DitariH),
15,227 sales-journal rows (DitariD). Verified on pos.spacecode.tech: `/articles` shows
"2184 artikuj gjithsej", `/blerjet` supplier dropdown lists the real suppliers, `/sale`
shows real products, `/faturat` 200. The old throwaway test row ("Papuqe 0304" + its 1 sale)
was truncated first; POSUsers (admin login) left intact.

**How it was done — a Python ETL, NOT a text sed/pgloader** (`scratchpad/etl.py`, kept for
re-runs): the SSMS dump has SQL-Server idioms that need real parsing — `CAST(0x… AS DateTime)`
**binary** datetimes (8 bytes: int32 days since 1900-01-01 + uint32 ticks@1/300s), `bit` 0/1,
`N'…'` strings, `image` blobs. The script is **schema-driven**: it reads the *target* Postgres
schema from `information_schema` (per-table `schema_<T>.tsv` = name/type/maxlen) and converts
each value to the exact target type — bit→bool, binary-datetime→`'YYYY-MM-DD HH:MM:SS'`,
bytea(Foto)→NULL, over-length varchar→truncated (e.g. EF made `IRregullt`/`PaBarkod`
`varchar(1)` while source is `varchar(100)` → `'False'`→`'F'`), ints tolerate `3.0`. Source
and EF column names/casing match 1:1 (via the `[Column]`/`[Table]` attrs incl. UPPERCASE
DitariD/DitariH), so name-based projection just works. Emits batched 500-row INSERTs +
`setval` to keep the identity sequence ahead. Pipeline: `iconv -f UTF-16LE -t UTF-8` →
`etl.py <T>` → scp `out_<T>.sql` → `docker exec … psql -f`.

### original source facts
`../POS2/script.sql` is the real SQL Server export (362MB **UTF-16LE**, `USE [master]…`,
56 `CREATE TABLE`, ~302k `INSERT` statements). It is **not git-tracked** (too large).
**It contains real production data**, inventoried (INSERT rows per table):
`Artikujt 2186` · `DitariD 15229` · `DitariH 2213` · `ArkaHyrjeDalje 4337` ·
`FurnitoriNew 97` · `Punetoret 5` · `Kategoria 3` · `Filiala 4` · `Sektori 4` · `Qytetet 20`
(POSUsers/BusinessPartners/ArkaHyrje/ArkaDalje/Borxhi = 0 rows in this export).

**Plan (per table, highest value first — start with `Artikujt` = the catalogue):**
1. `iconv -f UTF-16LE -t UTF-8` the dump (→ ~181MB UTF-8).
2. For each target table, extract its `INSERT [dbo].[T] (cols…) VALUES (…);` lines.
3. Transform T-SQL → Postgres and **project to the EF column subset only** — the SQL Server
   tables have more columns than the EF `[Column]`-mapped entities, so filter the column
   list + values to what the Postgres table actually has (names already match via the
   `[Column]`/`[Table]` attributes; `[ident]`→`"ident"`, `N'…'`→`'…'`, `bit 1/0`→`true/false`,
   datetime literals, `NULL` passthrough).
4. Load into the running Postgres (`pos-blazor-db`), identity columns via OVERRIDING.
5. Verify counts + spot-check the catalogue renders on `/articles` and `/sale`.
Best kept as its own careful pass; a column-diff (SQL-Server DDL vs EF model) drives step 3.

## Phase 6 — deployed & cut over ✅ (2026-07-08)
**LIVE at https://pos.spacecode.tech** (replaced the old React `../pos-web` — user
go-ahead 2026-07-08; old container kept on `:8120` as rollback, unrouted).
- **Self-provisioning:** `Program.cs` runs `db.Database.MigrateAsync()` on startup and
  seeds an `admin`/`admin` Admin user if `POSUsers` is empty (gate with `POS_SKIP_DB_INIT=true`).
  So a fresh container creates its 68-table schema and is immediately loginable.
- **Dedicated Postgres:** `docker-compose.yml` ships its own `pos-blazor-db`
  (`postgres:16-alpine`, volume `pos-blazor-pg`) — fully isolated from Supabase/pos-web.
  `POSTGRES_PASSWORD` in `.env` on Ampere (`~/apps/pos-blazor/.env`, gitignored).
- **App container** `pos-blazor` binds `127.0.0.1:8121` (pos-web used 8120).
- **Routing is the Cloudflare tunnel, NOT nginx.** The tunnel is token-mode
  (remotely-managed), so `deploy/nginx-pos-blazor.conf` is unused here — the ingress
  `pos.spacecode.tech → http://localhost:8121` was set via the CF API
  (`PUT .../cfd_tunnel/{id}/configurations`). Tunnels proxy Blazor's WebSocket
  automatically, so no extra config was needed.
- **Verified end-to-end over the public URL:** login `admin/admin` → 302 home,
  authenticated `/sale` and `/raportet` → 200; wrong password rejected; validated the
  same flow in a throwaway local Docker stack first.
- **Redeploy:** `dotnet publish -c Release -o publish` locally →
  `rsync -az --delete -e "ssh -i ssh-key-2026-07-01.key" publish Dockerfile docker-compose.yml deploy opc@143.47.190.37:~/apps/pos-blazor/`
  → `ssh … 'cd ~/apps/pos-blazor && sudo docker compose up -d --build'`.
- **Rollback:** repoint the CF ingress back to `:8120` (old pos-web still running).

## Phase 5 — hardware agent done ✅
The desktop hardware services are Windows/COM. Rather than reach the NAT'd cashier
PC from the server, the **browser** (which runs *on* that PC) fetches a tiny local
agent at `http://127.0.0.1:9099`. Payload generation stays server-side and testable.

- **`KosovaPOS.Agent.Contracts`** — dependency-free wire DTOs shared by server + agent.
- **Core `FiscalReceiptBuilder`** — pure F-Link `Fatura.inp` payload (ported from the
  desktop `GenerateFiscalReceipt`, no file I/O / logging). `HardwareMapper` turns a
  `Receipt`/`Article` into the agent requests.
- **`KosovaPOS.Agent`** — Kestrel loopback host (`/health`, `/fiscal/print`,
  `/receipt/print`, `/barcode/print`, `/scale/read`). Driver abstraction: **Windows**
  real drivers (fiscal = F-Link folder-drop + poll; receipt = ESC/POS raw via winspool;
  barcode = TSPL raw via winspool; scale = serial) vs **mock** drivers elsewhere / with
  `AGENT_MOCK=true`, so the whole pipeline runs off a Windows box. See `src/KosovaPOS.Agent/README.md`.
- **Browser bridge** — `wwwroot/js/hardware.js` (fetch, offline-safe) + `HardwareBridge`
  C# facade (JS interop). `Sale.razor` shows an agent-status badge and prints the fiscal
  receipt on completion; **printing is best-effort and never rolls back a saved sale**
  (agent offline → sale kept, cashier warned).
- **Verified on Linux (mock):** Core builder harness (payload asserts incl. VAT-group
  split + throws on invalid); agent endpoints via curl; and real domain objects →
  `HardwareMapper` → Blazor-defaults JSON → running agent (fiscal/receipt/barcode/scale
  all ok, correct `Fatura.inp`). Only the live browser JS-interop hop needs a real
  browser + Windows hardware to exercise (expected).

### Services to port (Core, no Windows deps — port directly)
ArticleService, ArticleDataService, SalesDataService, PricingEngine, ZReportService,
ATKReportService, LoyaltyService, KitchenService, AuditService, OfflineQueueService,
CloudBackupService, AppConfiguration, ArticleLookupCache, PosApiService, WebStoreSyncService.

### Services needing a client-side agent (Windows/COM/hardware — interface + browser agent)
FiscalPrinterService, ReceiptPrinterService, BarcodePrinterService, ScaleService,
CustomerDisplayService, A4InvoicePrintService, HRDocumentService (Word/Excel export).
ThemeService, NotificationService (MessageBox) → replace with web equivalents.

## Dev quickstart
```bash
# Postgres (local dev)
docker run -d --name pos-pg -e POSTGRES_PASSWORD=pos -e POSTGRES_DB=kosovapos \
  -p 5433:5432 postgres:16-alpine

# apply schema
export PATH="$PATH:$HOME/.dotnet/tools"
dotnet ef database update --project src/KosovaPOS.Core --startup-project src/KosovaPOS.Web

# run  (override DB via POS_DB_CONNECTION if not using the local default)
dotnet run --project src/KosovaPOS.Web
# → http://localhost:5199  · seed login admin / admin
```

## Deployment (Ampere, alongside existing stack)
Target URL **pos.spacecode.tech** — but that currently serves the live React
`../pos-web`. Build-out deploys to a **staging** host first; cut over only at
core parity.

```bash
# 1. build the runnable output locally (resolves the linked ../POS2 models)
dotnet publish src/KosovaPOS.Web/KosovaPOS.Web.csproj -c Release -o publish

# 2. ship to Ampere
rsync -a publish Dockerfile docker-compose.yml deploy ampere:~/apps/pos-blazor/

# 3. on Ampere: set the DB connection, build & run
ssh ampere 'cd ~/apps/pos-blazor && \
  echo "POS_DB_CONNECTION=Host=...;Database=kosovapos;Username=...;Password=..." > .env && \
  docker compose up -d --build'

# 4. nginx: install deploy/nginx-pos-blazor.conf (server_name pos-next.spacecode.tech),
#    ensure the map $http_upgrade→$connection_upgrade block exists in http{}, reload.
```
Notes: Blazor Server needs the WebSocket upgrade headers (in the nginx conf) and
`ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` (set in compose) for correct https
scheme behind the proxy. DataProtection keys persist in the `pos-keys` volume so
auth cookies survive redeploys.

## Known follow-ups
- **Cut-over to pos.spacecode.tech:** only after Sale + core screens reach parity
  with the live React app; needs explicit go-ahead (replaces a live service).
- **Decimal precision / column types:** review EF warnings before prod schema freeze.
- **Data migration** from the desktop SQL Server (BMDData) into Postgres.
- **Auth revalidation:** `PosAuthStateProvider` captures the principal at circuit
  start; consider DB revalidation for long-lived sessions / disabled users.
