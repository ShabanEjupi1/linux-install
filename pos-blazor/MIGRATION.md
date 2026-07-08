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
| 6 Deploy | Docker on Ampere behind nginx/tunnel, alongside existing stack | ⬜ |

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
