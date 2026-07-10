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
      Models/           the POCO models (source of truth since Phase 13)
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

## Phase 9 — hardware agent packaged for the shop PC ✅ (2026-07-09)
**The desktop is retired, so nothing prints fiscal receipts until the agent is on the
cashier PC.** Built the install package; running it is a human step at the shop.

```bash
./tools/publish-agent.sh     # -> publish/agent/KosovaPOS-Agent-1.0.0.zip (43 MB)
```

Cross-compiles a self-contained single-file **win-x64** exe from Linux — no Windows
box to build, no .NET runtime on the cashier PC. Then, on the shop PC from an
elevated PowerShell: `.\install-agent.ps1 -Mock` (hardware-free dry run, badge goes
amber) → `.\install-agent.ps1` (real, badge green) → `.\test-agent.ps1 -Fiscal`.
See [`src/KosovaPOS.Agent/deploy/INSTALL.md`](src/KosovaPOS.Agent/deploy/INSTALL.md).

Two fixes were needed to run as a service:
- **`AddWindowsService`** — without it the SCM has no control handler and `sc start`
  times out. The content root must also be pinned to `AppContext.BaseDirectory`
  (under the SCM the CWD is `C:\Windows\System32`), because the `IServiceCollection`
  overload does *not* set it — only `UseWindowsService` on `IHostBuilder` does, and
  `WebApplicationBuilder` can't use that.
- **File logging** — a service has no stdout, so a failed fiscal print left no trace.
  Daily-rolling files in `%ProgramData%\KosovaPOS\Agent\logs` (14 days), reported by
  `/health`. Framework logs filtered to Warning so the driver lines stay readable.

Still outstanding at the shop: apply the F-Link licence key, then install without `-Mock`.

## Phase 10 — shift & stock control ✅ (2026-07-10)
First slice of the ~23 desktop screens with no web equivalent. Scoped to the
cluster both tenants touch daily; "working, not full desktop parity".

**Open/Close shift were already done** — `/arka` + `ShiftService` covered both since
Phase 7. The only real gap was the **denomination counter**: `CashRegister.razor`
passed `denominationJson: null` to a service that already persisted it. Now `/arka`
counts €50→€0.10 notes/coins, and once any is entered the breakdown (not the typed
figure) becomes the counted cash, matching the desktop. Clearing the last denomination
back to zero also clears the total — otherwise the field unlocked still holding the old
count, and a drawer nobody had counted could close reporting a **0.00 difference**, an
apparently balanced till.

**New: `/inventari`** (from `POS/Windows/InventoryManagementWindow`) — stock in / out /
physical count, over a new **`StockMovement`** journal (`StockMovements` table, additive
migration). Two deliberate deviations from the desktop:
- Every change is journaled with before/after/delta/user/cost. The desktop only wrote a
  movement row when the unused `InventoryStocks` table was populated, so in practice its
  stock edits left **no audit trail at all**.
- **Stock-out can no longer overshoot into negative.** The desktop offered a "continue
  anyway?" prompt; that is one way the live DB ended up with **430 negative-stock
  articles**. The escape hatch is *Inventarizim* (physical count), which sets the counted
  figure and journals the delta. `/inventari` surfaces the negative count as a banner.

**New: `/barkod`** (from `POS2/Windows/BarcodeWindow`) — search → print queue with copies
→ print via the hardware agent (`/barcode/print`). Prints item-by-item so a jammed label
leaves the rest of the queue intact; failures stay queued. Shows the agent badge, and
disables printing when the agent is offline.

**Not ported — `POS2/Windows/StockValueAdjustmentWindow` ("RREGULLIMI I STOKUT PËR ATK").**
It takes a *target total stock value* and back-solves per-article quantities to hit it
(equal / weighted / random ±20% "so the distribution looks natural"), overwrites the real
`Artikujt.Sasia` with those invented figures, and exports them as a tax-authority document.
That is fabricating inventory records for ATK, not adjusting stock. The legitimate version
of the job — correcting stock from a real physical count — is the *Inventarizim* action on
`/inventari`.

Verified on Linux against a throwaway Postgres: 20/20 assertions on `StockService`
(in/out/count math, `SasiaHyrje`/`SasiaDalje` counters, unit-cost capture, journal order and
per-article filter), the overshoot refusal **rolls back leaving no journal row**, a count
correction lifts `-6 → 4` and clears the negative, denominations round-trip through
`CloseShiftAsync` (1×50 + 2×20 + 1×0.50 = 90.50, diff 40.50). Pages render authenticated
(`/inventari`, `/barkod`, `/arka` → 200) with the negative-stock banner correctly
conditional. Not exercised: the interactive Blazor circuit (modal clicks) and real label
hardware — both need a browser + the shop PC.

## Phase 13 — `Tatimi` is a VAT class, not a rate ✅ (2026-07-10)
`Artikujt.Tatimi` was read as a VAT **percentage**. It is a **class code**. The catalogue
stores `Tatimi = 3` on 1,466 of its 1,619 articles, so the till was carving **3% VAT** out
of their gross prices instead of 18%:

| `Vat` | `Tatimi` | articles | old rate | new rate |
|---|---|---|---|---|
| 3 | 3 | 1466 | **3%** ❌ | 18% |
| 3 | 18 | 146 | 18% | 18% |
| 4 | 4 | 6 | **4%** ❌ | 18% |
| 5 | 5 | 1 | **5%** ❌ | 18% |

Part of the catalogue does carry the literal percentage in `Tatimi` (the 146 rows above),
which is why the column reads plausibly as a rate. `KosovoVat.Resolve` disambiguates on
magnitude: **8 or more can only be a percentage** — no class code reaches that high — so
it is taken verbatim, and anything else is read as a class. Classes 4 and 5 do not exist
in Kosovo (the rates are 18 / 8 / 0); they fall back to the standard rate, because
under-charging VAT is the costly direction of the error. All 7 are out of stock.

**Nobody was overcharged.** Shelf prices (`CShitjes`) are gross — VAT is carved out of the
total, never added on top — so `Total` is byte-for-byte unchanged. What was wrong is the
**net/VAT split**: the figure reported to ATK, and `FiscalReceiptBuilder.GetTaxGroup`,
which mapped `3 → group 3` and would have stamped nearly the whole catalogue into the
wrong fiscal tax group. Fiscal printing is not live yet (Phase 9 still awaits a human on
the shop PC), so no fiscal receipt ever carried the bad group.

The VAT arithmetic that was inlined and duplicated across `CatalogService`, `SalesService`
and `Receipt` now lives in one place, `KosovaPOS.Core/Models/VatRate.cs`:

- `Resolve(vat, tatimi)` → the rate, per the magnitude rule above.
- `VatOf(gross, rate)` → the VAT **inside** a gross amount, rounded to cents.
- `NetOf` / `NetUnitPrice` → the net part. Unit prices stay **unrounded**; rounding them
  would stop `qty × unit` from reconciling with the rounded line total.

`SalesService` now writes `VleraPaTvsh = TotalValue - VATValue` rather than recomputing
the net from the rate, so the persisted `net + VAT` equals `gross` exactly, to the cent.

⚠️ **Historical sales carry zero VAT and must not be backfilled** — see the BMDData notes.
The 18,749 imported rows predate this app; their `Tvsh` is 0 in the source system.

### Negative stock is a fault, not a valuation
430 articles carry negative `Sasia` (they were sold without ever being booked in — a
source-data fault inherited from the desktop). `/stoku` multiplied those negatives by unit
price, so **broken records were subtracting real money from the stock valuation**. Stock
value is now summed over positive quantities only, negative rows render `—` rather than a
negative euro figure, and a banner counts them and points at `/inventari` to correct them.
`Pa stok` now means exactly zero; negative rows get their own counter and filter, so they
stop hiding inside the out-of-stock count.

### The models come in from the cold
`KosovaPOS.Core.csproj` used to `<Compile Include="..\..\..\POS2\Models\**\*.cs" />`, so the
POCO models — including the whole VAT fix above — were compiled from a tree that **this repo
never tracked**. `POS2/` is a working clone of `github.com/ShabanEjupi/POS.git`, a *separate*
git repo, which is why the outer repo only ever saw an opaque embedded repo and silently
staged nothing. `pos-blazor` could not be built from a clean checkout, and the fix existed
only on one Codespace disk.

The 39 files now live at `KosovaPOS.Core/Models/` and the link is gone; the project builds
from its own sources (`dotnet msbuild -getItem:Compile` reports zero `POS2` paths). The
desktop app is retired, so its copy is left where it is and nothing has to stay in sync —
**`pos-blazor` is the source of truth for the models now.**

Verified against the real `(Vat, Tatimi)` pairs from the live DB by driving `KosovoVat`,
`ReceiptItem` and `GetTaxGroup` directly (24 assertions): every live pair resolves to 18%,
class round-trips hold, and `net + VAT == gross` across discounts, the 8% rate, zero-rating
and pathological rounding (13 × 0.07 → gross 0.91, VAT 0.14, net 0.77). Re-run green after
the relocation. Not exercised: the interactive Blazor circuit.

## Phase 12 — the till and stock screens get permissions ✅ (2026-07-10)
Phase 11 gated the six screens the desktop had gated. Seven were left on a bare
`@attribute [Authorize]`: `/sale`, `/faturat`, `/kthimet`, `/arka`, `/stoku`,
`/inventari`, `/barkod`. The desktop never gated these either — one cashier per
station, so it never came up — so there was nothing to port and the gap was invisible.

It surfaced the first time a second role existed: an **Accountant** (`fitim`, granted
only reports / finance / purchases / partners) could open the till, refund a sale,
close the shift and rewrite inventory quantities. Only `/perdoruesit` and `/articles`
turned him away.

The existing permission flags could not express this — none of them means "may sell" or
"may touch stock". Two new columns on `POSUsers`, `CanSell` and `CanManageStock`, feed
two new permissions (`sell`, `stock`) and two new checkboxes on `/perdoruesit`:

| screens | permission | column |
|---|---|---|
| `/sale` `/faturat` `/kthimet` `/arka` | `perm:sell` | `CanSell` |
| `/stoku` `/inventari` `/barkod` | `perm:stock` | `CanManageStock` |

Migration `AddSellAndStockPermissions` adds both columns as `false`, which would lock
every existing user out of screens they use daily, then backfills from the role presets
(`CanSell` for Admin/Manager/Cashier, `CanManageStock` for Admin/Manager/Warehouse).
Nobody gains access they did not already have.

**Everyone is signed out by this deploy.** `BuildPrincipal` bakes permission claims into
the auth cookie at sign-in, so cookies issued before this deploy lack `perm:sell` and
`perm:stock` and would be denied the till. The cookie name is bumped to
`KosovaPOS.Auth.v2` to force one clean re-login. (Contrast Phase 11, which only *read*
claims that were already being issued.)

The first-run seed no longer creates `admin`/`admin`. It reads `POS_SEED_ADMIN_USER` /
`POS_SEED_ADMIN_NAME` / `POS_SEED_ADMIN_PASSWORD`, and with no password set it generates
a random one and logs it once — there is no well-known default password any more.

Verified against a throwaway Postgres seeded at the *previous* migration with four users,
then migrated forward and driven over real cookie logins. Backfill landed exactly right
(Accountant neither, Cashier sell-only, Warehouse stock-only, Admin both). All 13 gated
routes probed per role: Admin 200 everywhere; Accountant 200 on exactly
`/blerjet` `/financat` `/raportet` `/partneret` and 302 → `/nuk-keni-leje` on the other
nine; Cashier 200 on the four till routes only; Warehouse 200 on the three stock routes
plus `/articles` `/blerjet` `/partneret`. Not exercised: the interactive Blazor circuit.

**Consequence worth knowing:** a Cashier can no longer open `/stoku` to look up stock on
hand. If that bites at the till, either tick `Menaxho stokun` for that cashier or split
the read-only `/stoku` onto a policy that accepts `sell` **or** `stock`.

## Phase 11 — nav shell, design pass, and an authz fix ✅ (2026-07-10)
Until now every screen was reached from the tiles on `/` and navigated away from with
a `← Ballina` link. With 14 screens that had stopped scaling.

**Nav shell.** `MainLayout` grew a persistent sidebar (grouped Shitja / Stoku / Financa
/ Sistemi), an app bar carrying the signed-in user and `Dil`, and a mobile drawer. The
layout renders in **static SSR** even when the page inside it is `InteractiveServer`, so
the drawer cannot use `@onclick` — it is a pure-CSS checkbox toggle, which also resets on
navigation. The per-page `← Ballina` links are gone. `/` is now a dashboard (today's
revenue / receipt count / items sold, shift state) rather than a wall of tiles.

New `EmptyLayout` for pages that own the viewport or may render with no session (login,
`/Error`, 404). It deliberately does not inject `TenantService`, so an unauthenticated
404 costs no DB query.

**Fixed a real authz hole — and five more like it.** `/perdoruesit` — create users, set
roles, reset passwords — carried only `@attribute [Authorize]`. Any authenticated user,
including a **Cashier**, could open it by typing the URL; the Home tile was hidden from
them, and hiding the link was the entire "protection". An audit found the same weakness on
`/raportet`, `/financat`, `/blerjet`, `/partneret` and `/articles`, each of which the
desktop gated behind a permission.

`AuthService.AllPermissions` is now the single source: startup registers a `perm:{name}`
policy for each, and every page carries the matching
`[Authorize(Policy = "perm:…")]`. The policies check the `perm` claims `BuildPrincipal`
has always issued, so **nobody is signed out by this deploy**. `Admin` short-circuits
`HasPermission`, so the live `admin` account keeps everything.

Two paths land a signed-in user who fails a policy, and both needed handling:
- Page-level `[Authorize]` is enforced by the **endpoint** on the SSR request, which
  redirects to `CookieAuthenticationOptions.AccessDeniedPath` → new `/nuk-keni-leje`.
- The router's `<NotAuthorized>` fragment catches the rest. It used to unconditionally
  `<RedirectToLogin />`, which for an *already signed-in* user is a redirect loop back to
  the page they cannot see. It now branches on `IsAuthenticated` and shows a 403.

`/nuk-keni-leje` itself is `[Authorize]`: only a signed-in user is ever sent there, and
without it an anonymous visitor got the business name, the nav shell and a `TenantService`
query. An anonymous hit redirects to `/login`, so there is no loop.

The sidebar and the Home tiles hide exactly what the policies deny — a visible link that
403s is a dead end. The `Sistemi` heading is hidden too, since every link under it is
gated and a Cashier would otherwise see an empty section.

Verified end-to-end against a throwaway Postgres, driving real cookie logins over HTTP for
a seeded no-permission Cashier and for `admin`. Admin: 200 on all 15 routes. Cashier: 200
on the seven till routes (`/`, `/sale`, `/faturat`, `/kthimet`, `/stoku`, `/inventari`,
`/barkod`, `/arka`) and 302 → `/nuk-keni-leje` on all six gated ones, the chain
terminating in **one** redirect at a rendered 403 (no loop). The Cashier's rendered nav
and tiles resolve to exactly the set that returns 200. Anonymous still goes to `/login`
everywhere, including `/nuk-keni-leje`. Not exercised: the interactive Blazor circuit
(button clicks inside a page) — needs a browser.

**Note.** The live DB holds a single user (`admin`, Admin, all flags). The hole was
therefore not yet exploitable in production; it would have opened silently the moment the
first Cashier account was created.

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
# 1. build the runnable output locally
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

## Phase 14 — partners come from FurnitoriNew; the un-migrated BMD tables land

`/partneret` read the `BusinessPartners` table, which BMDData never populated, so
it rendered an empty list forever — while `/blerjet`'s supplier dropdown, reading
`FurnitoriNew`, worked fine. PartnerService now reads and writes `FurnitoriNew`;
`BusinessPartner` survives only as the page's view model. Two of its fields have
no column behind them and are no longer editable: `Balance` is summed from the
new `Kartela_Subjektit` ledger, and `IsActive` has no BMD equivalent.

Eight tables the export/import scripts had never covered are now included:

| Table | Rows | Why |
|---|---|---|
| `tbl_Stoku` | 17,795 | Stock ledger — the audit trail behind `Artikujt.Sasia` |
| `Kartela_Subjektit` | 4,335 | Partner account ledger; `sum(Mbeti)` = outstanding balance |
| `NjesitMatese` | 14 | Units of measure |
| `KategoriaPos` | 7 | POS categories (`Image` blob dropped, as with `Artikujt.Foto`) |
| `LlojiShpenzimeve` | 6 | Expense types |
| `MetodaPagese` | 4 | Payment methods |
| `Arkat` | 4 | Cash registers (two rows are `xcv` test junk from the desktop) |
| `Tatimi` | 3 | VAT classes — **reference data only**, see below |

**Do not derive VAT rates from the `Tatimi` table.** Its IDs (3, 4, 5) line up
exactly with the values `Artikujt.Vat` holds, so it reads like a foreign key —
and under that reading 1612 of 1619 articles are "Pa Tvsh" (0%), which would also
explain why the whole sales history carries zero VAT. But the shop **is**
VAT-registered: those flags are stale, and the class-3 population includes plainly
standard-rated goods (tricycles, blouses, keys). `KosovoVat.Resolve` stays
authoritative.

Deliberately **not** migrated: `RazlCeni` (249,623 rows) and `KatArt` (994) are a
legacy import from a different, Macedonian-language ERP; `Artikujt_Backup`,
`Sheet1$`, `DitariDTemp` and the `Pompa*` fuel-pump module are dead weight; and
`Kompania`'s single row is placeholder junk (`NF=60000000`, `Tvsh=16`) that must be
re-entered by hand before it can ever feed a fiscal receipt.

`import-bmddata.py` now skips (loudly) any table with no CSV in the export dir, so
a partial backfill of just the new tables is a supported run.

## Known follow-ups
- **Cut-over to pos.spacecode.tech:** only after Sale + core screens reach parity
  with the live React app; needs explicit go-ahead (replaces a live service).
- **Decimal precision / column types:** review EF warnings before prod schema freeze.
- **Data migration** from the desktop SQL Server (BMDData) into Postgres.
- **Auth revalidation:** `PosAuthStateProvider` captures the principal at circuit
  start; consider DB revalidation for long-lived sessions / disabled users.
