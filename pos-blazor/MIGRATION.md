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

## Phase 15 — Settings screen; the Enable* flags gate nothing

`/cilesimet` (perm:settings — Admin or Manager) is the first UI over
`BusinessSettings`, the singleton row the shell and the non-fiscal receipt read.
It edits exactly four fields, because those are the four anything consumes:

| Field | Consumed by |
|---|---|
| `BusinessName` | sidebar brand, app-bar title, receipt header |
| `Address` | receipt header |
| `FiscalNumber` | receipt header **only** — see the warning below |
| `Profile` | sidebar sub-label; `TenantService.IsRestaurantAsync()` |

**The 20 `Enable*` toggles have no consumers and are deliberately not rendered.**
A grep across `src/` finds exactly two references outside the model and the
migrations — `EnableTableManagement` and `EnableKitchenDisplay`, both inside
`IsRestaurantAsync()`, which nothing called before this phase. There are models
for Loyalty, GiftCards, Rentals, Appointments, KitchenOrder, Delivery, Bundles,
Variants, PriceRules and PurchaseOrders, but no screens and no code that reads
the flag. Shipping the switches now would let an operator "enable" features that
can never turn on. **Each toggle lands in the commit that implements the feature
it gates, not before.**

`SaveSettingsAsync` therefore copies field-by-field onto the tracked row rather
than attaching the form's instance: the form binds four columns, and replacing
the whole row would reset the other ~24 (including `RestApiKey` and any toggle a
future screen has set) to their CLR defaults.

⚠️ **`BusinessSettings.FiscalNumber` is not the fiscal device's number.** It is
printed on the courtesy receipt. What ATK sees comes from `FISCAL_NUMBER` in the
agent's service environment on the shop PC (`install-agent.ps1`). The two are
independent, and the screen says so.

## Phase 16 — multi-business: one database per business ✅ (2026-07-10)

The platform now serves many businesses. Isolation is the **database boundary**,
not a `TenantId` column: there are still zero tenant columns across the 70 DbSets,
and there never will be. A query that forgets to filter by business cannot reach
another business's rows, because it is not connected to them.

**The seam.** All 12 domain services already injected
`IDbContextFactory<PosDbContext>` and only ever called `CreateDbContextAsync()`.
`AddDbContextFactory<PosDbContext>` is gone; a scoped `TenantDbContextFactory`
takes its place and resolves the database from the signed-in user's `bizid` claim
on every context it opens. **No service changed.** It has no default and no
fallback — a scope with no business gets `NoBusinessInScopeException`, never the
primary database.

**Why the claim and not the URL.** The claim is signed by DataProtection, so it
cannot be forged, and it survives a Blazor Server circuit:
`PosAuthStateProvider` captures the principal while the initial HTTP request is
still in flight, which is the only reason `HttpContext` being long gone by query
time does not matter. The claim carries the business *id*; the database name is
looked up in `BusinessRegistry` on every resolve, so **deactivating a business
locks out its live sessions within the 30s cache TTL** rather than waiting out
their 12-hour cookies. A deactivated session is signed out and bounced to
`/login` by middleware, not shown a 500.

**Control database (`pos_control`).** Its own context, its own migration history:
`Businesses` (Code, Name, DatabaseName, IsActive) and `PlatformAdmins`. Holds no
business data. `Code` and `DatabaseName` are both unique — two rows sharing either
would silently cross-wire two businesses.

**Login** grew a "Kodi i biznesit" field, remembered in the `KosovaPOS.Biz` cookie
so a till types it once. Unknown business, unknown user and wrong password all
return **one** message: distinguishing them turns the form into an oracle for
which businesses exist. `AuthService.AuthenticateAsync` now takes the `Business`
as an argument — at login there is no signed-in user to resolve one from, so it is
the one service that must not use the tenant-aware factory.

**Platform admins** sign in at `/admin/login` and manage businesses at
`/admin/bizneset`. They hold `platform` and **no `bizid`**, so the factory opens
no business database for them at all — reaching business data will be a deliberate,
audited impersonation step (Phase 17), not a side effect of being an admin. `/` is
now gated on the `business` policy so they cannot land on a dashboard whose first
query has nowhere to run.

**Provisioning** (`BusinessProvisioner`) does CREATE DATABASE → migrate → seed
Admin → register, in that order. Registered **last**: an unregistered database is
inert and the operation is re-runnable, whereas a registry row pointing at a
database that failed to migrate is a business whose users can log into a broken
app. It **never drops a database** — a rollback path is one bug away from deleting
a live shop.

⚠️ **`PgIdentifier` is the entire defence against SQL injection here.** A database
name is an identifier, not a value, so `CREATE DATABASE` cannot parameterize it and
the name is concatenated into the statement. The class allowlists
`^[a-z][a-z0-9_]*$` rather than escaping, and reserves `control`/`postgres`/
`template0`/`template1`/`admin` — `control` would otherwise provision `pos_control`
straight on top of the registry. Verified: `bmd"; DROP DATABASE kosovapos; --` is
refused. Codes are lowercased before validation, so `UPPER` is accepted **as**
`upper`, matching what login does with the same input.

**Rollout is a no-op for the existing shop.** On first boot with an empty registry,
the app adopts the database `POS_DB_CONNECTION` already names as business #1 under
`POS_PRIMARY_BUSINESS_CODE` (`bmd`), taking its name from `BusinessSettings`. No
data moves. Its users simply start typing a code. The auth cookie is bumped to
`KosovaPOS.Auth.v3` because a v2 cookie names no database.

🚨 **`NpgsqlCompat.EnableLegacyTimestampBehavior()` — a global that used to turn on
by accident.** The switch was set in `PosDbContext`'s *static constructor*, so it
only applied once something touched that type. `ControlDbContext` is opened first
at startup, so the switch was still off and writing `DateTime.Now` (Kind=Local)
threw; the control migration also scaffolded as `timestamptz` while the app writes
`timestamp`. Both contexts and every entry point (web host, both design-time
factories) now call it explicitly. **`dotnet ef` must scaffold under the same
mapping the app runs with**, which is why the design-time factories set it too.

Migrations now need `--context`, and design-time factories exist for both contexts
because the tooling can no longer pull options out of the host's DI:

```bash
dotnet ef migrations add <Name> --context PosDbContext \
  -p src/KosovaPOS.Core -s src/KosovaPOS.Web
dotnet ef migrations add <Name> --context ControlDbContext \
  -p src/KosovaPOS.Core -s src/KosovaPOS.Web -o Migrations/Control
```

Startup migrates every active business serially. That is a loud, early failure for
a handful of shops; **it makes container start time grow with the customer count**,
so move it to a background service before this serves dozens.

`TenantService` was renamed `BusinessProfileService`. It selects which *UI* a
business sees (Retail vs restaurant) and was never tenancy — with a real tenant
boundary in the codebase, the old name was a landmine.

Verified end-to-end against a throwaway Postgres: shop A's article is invisible to
shop B over HTTP and vice versa; a platform admin is refused `/`; a business user
is refused `/admin/bizneset`; deactivation bounces a live session to `/login` while
the other shop is unaffected; reactivation restores it.

### Per-business subdomains — `pos-<code>.spacecode.tech`

Each shop now has its own hostname; the apex `pos.spacecode.tech` keeps the code-field
login and hosts `/admin`. The design goal was **zero per-shop ops**: creating a business
in the console makes its URL work immediately.

- **First-level, `pos-` prefixed, on purpose.** Cloudflare's free Universal SSL covers the
  apex and *one* subdomain level, so `pos-bmd.spacecode.tech` is inside the free
  `*.spacecode.tech` cert; `bmd.pos.spacecode.tech` would need paid ACM. The `pos-` prefix
  keeps shop hosts out of the infra namespace (mail/git/ssh/audit). And cloudflared only
  wildcards a rule starting with `*.`, so `pos-*` can't be expressed at the edge anyway —
  the prefix scoping lives in `BusinessHostResolver`, which resolves a Host header to a
  business code and rejects anything that isn't `pos-<valid-code>` exactly one level deep
  (`pos-bmd.evil.spacecode.tech` → no match).

- **The Host header resolves the business; the `bizid` claim still owns the data.** On a
  business subdomain the login form hides the code field and pins the business from the
  host — a POST that tries to smuggle a different code onto `pos-bmd` is overwritten with
  `bmd` in `OnInitializedAsync` (which runs after form binding) before it is used.

- **Two layers keep one shop's session off another's host.** The auth cookie is host-only
  (no `Domain` set), so a cookie minted on `pos-bmd` is never sent to `pos-enisi`. Behind
  that, a middleware refuses to serve any session whose `bizid` doesn't match the host's
  business and bounces it to that host's `/login` — without clearing the real session on
  its own host. Verified: a valid bmd session sent to `pos-enisi` is rejected, then still
  works on `pos-bmd`; login on each subdomain needs only username + password and shows the
  right shop's name.

- **Ops is one DNS record + one tunnel rule** (`pos-blazor/deploy/subdomains.md`): a proxied
  `*` CNAME on the zone, and a `*.spacecode.tech` cloudflared ingress rule placed *after*
  the specific infra hostnames. nginx `server_name` is now a regex matching `pos-<label>`
  and bare `pos`. Config: `POS_BASE_HOST` / `POS_HOST_PREFIX`.

  ⚠️ The wildcard makes the POS the catch-all for any otherwise-unmatched first-level
  subdomain on `spacecode.tech`. Acceptable (the app only answers hosts it recognises), but
  if you'd rather scope tightly, drop the wildcard and create one `pos-<code>` CNAME per
  shop at onboarding instead.

## Phase 18 — the audit trail, and a lock on the front door

The `AuditLogs` table has existed in every business database since `InitialCreate` and
**nothing had ever written a row to it.** Impersonation, refunds, price changes, shift
closes, logins: none of it was recorded anywhere. Meanwhile the login form answered an
unlimited number of guesses per second, on a public URL, for every business at once.

### Two logs, because there are two kinds of actor

Acts inside a shop are written to **that shop's own database** — the evidence lives with
the data it describes, and a shop can be handed its own history. Acts by a **platform
operator** go to the control database, because they either touch no business database (a
platform login) or must outlive one being deleted. **Impersonation writes to both**: the
control DB records that an operator reached into a shop, the shop's DB records that it
was reached into. Neither side holds the only copy.

`AuditLog.ImpersonatedBy` is the column that makes an impersonated act legible: `UserName`
stays the business user (behaving as them is the *point* of impersonation), so without
this column a sale rung up by a platform operator is indistinguishable from one the
cashier made. `/auditimi` renders it as a 👁️ badge next to the name.

What is recorded: login, failed login, lockout, logout, sale, return, shift open/close,
article create/update/delete (with a real before→after price delta, captured from the
untouched grid row), stock adjustment, purchase create/update/delete, user create/update/
deactivate (with a permission delta — `+shitje`, `−përdoruesit`), settings save, business
create/activate/deactivate, impersonation start/end.

**Audit writes are best-effort** (logged and swallowed, never thrown). A Postgres hiccup
in the audit table must not refuse a customer's cash. That is a deliberate trade: this is
an investigative record, not a fiscal ledger. If it is ever promoted to evidence the tax
authority relies on, the sale and its audit row have to share one transaction — today
they do not.

Reading the log is gated on a new `audit` permission (Manager or Admin), deliberately
*narrower* than `users`: someone who can create cashiers should not automatically get to
read what everyone in the shop has been doing.

### Login lockout (`LoginThrottle`)

Per account (business + username): 5 failures in 15 minutes → 5-minute lock, doubling to
a 60-minute cap. Short on purpose — five bad passwords is usually a cashier at a till, and
a shop that cannot sell for an hour is worse than the attack it prevents. Per IP: 30
failures in 15 minutes → 15-minute lock, which is what stops one password being sprayed
across every username. ⚠️ **A shop's tills share one NAT address**, so that threshold is
far above honest use on purpose; lowering it can take a whole shop offline. The platform
console is throttled in its own namespace (`" platform"` — the leading space cannot
collide with a business code), so guesses at the console can never lock out a till.

State is in-memory: a restart clears every lock. Acceptable — an attacker cannot force a
restart, and persisting a row per failed guess would hand them a way to fill the disk. It
also means attempts made *while* locked are not audited (the lock row is), which is what
stops the log being floodable.

Verified end-to-end in a browser against a throwaway Postgres: the 5th wrong password
trips the lock and **the correct password is then refused**; a platform operator's
impersonated sale lands in both logs with `ImpersonatedBy = platform`; a Cashier who types
`/auditimi` gets `/nuk-keni-leje` and never sees the nav link.

### 🚨 `Receipts` is a dead table — sales live in `DitariD`

The roadmap carried a follow-up to give `Receipt` an `ImpersonatedBy` column. It was built,
and then reverted, because **nothing in the codebase reads or writes the `Receipts` table**:
`SalesService.SaveReceiptAsync` persists a sale as journal rows in the legacy `DitariD`,
and `Receipt` survives only as the in-memory object the Sale screen builds and the receipt
views render. The column would have been a column on a table nobody writes — worse than
useless, because it would *look* like receipts recorded the operator. The SALE audit row
carries `ImpersonatedBy` instead, and that is now the durable record. `PosDbContext` warns
about this at the `DbSet` so the next person does not rebuild it.

## Phase 19 — printing: the receipt was never on 80mm paper

User: *"the printing of receipt is bad — my last app used to print it exactly as it was on the
Epson TM-T, and barcodes printed fine on the HPRT."* Two separate bugs and one missing feature.

### 🚨 `@page` is a GLOBAL rule, and `size: 80mm auto` is invalid CSS

`app.css` held **two** `@page` rules — `size: A4` (for `/fatura`) and `size: 80mm auto` (for
`/kupon`). `@page` is not scoped to the selectors around it: it is a document-level rule, so
those two fought, and a receipt could be laid out for A4. Worse, **`size: 80mm auto` is not
valid CSS at all** — the paged-media spec allows `auto`, or one or two lengths, but *not* a
length mixed with `auto` — so Chrome dropped the declaration and fell back to Letter. Proof:
Chromium rendered `/kupon` to a **612×792pt (Letter)** PDF.

Fix: **no `@page` may live in `app.css`** (there is a loud comment there now). Each printable
page declares its own inside `<HeadContent>`, which is only in the DOM while that page is. The
receipt's paper **length is computed from the line count** (font-size × line-height), so the
page is a real `80mm × Nmm` and the thermal printer feeds no blank paper past the cut. Now
renders to **227×210pt = 80.1mm × 74.1mm**.

### The layout is now built once, as a monospace grid

The desktop drew receipts with **GDI, Courier New, through the Windows driver**
(`POS2/Services/ReceiptPrinterService.cs`) — a fixed 40/42-column grid. The web page instead
used flexbox rows and `font-size: 12px`, so nothing lined up and print scaling moved it around.
New **`Core/Printing/ReceiptDocument.cs` (`ReceiptFormatter`)** lays a sale out as
**42-column** lines (80mm roll = 72mm printable = 42 Courier characters), amounts right-aligned
into the grid; `Receipt80.razor` only renders them. **Sizes are in `mm`, never `px`** — px drifts
with the print scale factor, mm lands the same at 180dpi and 203dpi.
⚠️ The agent's ESC/POS driver has **not** been moved onto this formatter yet (different input
shape, and no shop runs the agent today) — so the two can still drift. Do it when the agent lands.

### Barcode labels, without the agent

The desktop sent raw **TSPL** to the HPRT (`BARCODE x,y,"128",…`) and the printer's firmware drew
the bars. **A browser cannot send raw bytes to a printer**, so `/barkod`'s print button was dead
on every PC in the shop (it required the agent). New **`Core/Printing/BarcodeSvg.cs`** encodes
**EAN-13 / EAN-8 / Code 128** and draws the bars as SVG in **millimetres** (narrow bar 0.33mm ≈
the 2-dot bar the desktop asked TSPL for); new **`/etiketa`** renders 55×25mm label pages —
company / name / big price / barcode, mirroring `BarcodePrinterService.GenerateTSPLLabel` — and
prints them through the HPRT's own Windows driver. This is the same escape hatch the desktop had
(`GenerateTSPLWithBitmap`): same picture, different transport. A code that cannot be encoded
(non-ASCII, or 13 digits with a bad check digit) prints as **text with no bars**, and the screen
says so — printing a barcode a scanner will refuse is worse than printing none.

**Verified by decoding the actual print output, not the screen:** Chromium → PDF (`prefer_css_page_size`)
→ 300dpi raster → **zxing** decoded `EAN13 = 5901234123457` and `Code128 = NO.35` off the labels,
and the label pages measured **156×71pt = 55×25mm**. *A barcode that renders is not a barcode that
scans — decode it.*

## Phase 20 — printing with no print dialog, and the agent ships from the POS ✅ (2026-07-12)

User: *"I don't want these instructions — print it without the print dialog."* The instructions
("Margins → None, untick Headers and footers") were a symptom. **No web page can suppress Chrome's
print dialog.** Only two things can, and both are now in place.

**1. The agent is the default path.** `/kupon/{n}` and `/etiketa` try the agent FIRST — raw ESC/POS
to the receipt printer, raw TSPL to the label printer, no dialog — and fall back to `window.print()`
only on a PC without it. `/barkod` lost its browser-vs-agent button pair; the page decides. Labels
fall back all-or-nothing: if the *first* label fails nothing has printed yet, so the queue is safe to
re-send; a failure *after* one printed is reported instead, or the shop gets duplicates.

**2. `tools/kiosk-shortcut.ps1`** (fallback for a PC that cannot run the agent): a Desktop shortcut
launching Chrome `--kiosk-printing` in its own profile, so `window.print()` prints instantly.
⚠️ Kiosk printing always goes to the Windows **default printer** — a page cannot choose one — so it
cannot route receipts vs labels on a 2-printer PC, and the A4 invoice would come out of the thermal
as a ribbon. Stopgap only.

### 🐛 The agent would have failed on its very first print
Nothing ever called `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)`, and .NET Core
ships only UTF-8/ASCII/Latin1 — so `Encoding.GetEncoding(1252)`, used by **both** the ESC/POS and the
TSPL encoder, throws `NotSupportedException`. **Every receipt and every label would have failed** with
*"No data is available for encoding 1252"*. It had never been caught because the agent had never been
installed anywhere. Fixed in `Agent/Program.cs` + a `System.Text.Encoding.CodePages` PackageReference.

Also: the agent's TSPL hardcoded **SIZE 40mm × 30mm** while the browser prints **55×25** — labels
would have come out cropped. The stock now travels in `BarcodePrintRequest`, and the module width
shrinks to fit (falling back to text when even the thinnest bar won't, exactly as the browser does).

### The receipt had two layouts; now it has one
`ReceiptPrintRequest` no longer carries the sale (items, totals, header) — it carries the **already
formatted lines** from `ReceiptFormatter`, and the agent only stresses and emits them. The agent used
to lay the receipt out a *second* time, so the paper and `/kupon/{n}` could drift apart silently. One
`HardwareBridge.PrintReceiptAsync(long receiptNumber)` now serves both the till and the reprint page,
laid out from what was persisted — so a reprint months later is the same document.

### The shop PC installs everything from the POS itself
Nothing is carried to the cashier PC by hand any more.

- **`/pajisjet`** (Cilësimet → Pajisjet): shows whether this PC has the agent, takes the two printer
  names, and hands over a one-line command with **this shop's own host and printer names already in
  it**. The host matters: the agent's CORS policy trusts exactly the origin it was installed with, so
  a shop on `pos-<code>.spacecode.tech` must not install an agent that only trusts the apex.
- **`/shkarko/{agjenti.zip,instalo.ps1,kiosk.ps1}`** — anonymous by design (PowerShell carries no auth
  cookie, and a login there would break the one-command install). The served names are a fixed
  whitelist mapped to fixed files, so no crafted name can walk out of the package directory.
- **`tools/build-release.sh`** is now the deploy build: it publishes the web app *and* stages the agent
  package into `publish/agent-package/`. A plain `dotnet publish` ships **no package**, and `/pajisjet`
  says so rather than offering a download that 404s.

```bash
./tools/build-release.sh
rsync -az --delete publish Dockerfile docker-compose.yml deploy ampere:~/apps/pos-blazor/
ssh ampere 'cd ~/apps/pos-blazor && sudo docker compose up -d --build'
```

**Verified:** agent runs and accepts both new payloads; TSPL rendered for the real 55×25 stock (EAN-13
centred, narrow=3) and for an unfittable CODE128 (falls back to text); ESC/POS bytes dumped (`ESC @`,
`GS ! 01` title, `ESC E 01` total, `ë` = 0xEB, `GS V B` cut); the published app served the 43MB zip as
a valid archive containing the freshly-built exe, with unknown names and traversal 404ing and
`/pajisjet` redirecting to `/login`. **Not run on real hardware — that still needs the shop PC.**

## Known follow-ups
- **Cut-over to pos.spacecode.tech:** only after Sale + core screens reach parity
  with the live React app; needs explicit go-ahead (replaces a live service).
- **Decimal precision / column types:** review EF warnings before prod schema freeze.
- **Data migration** from the desktop SQL Server (BMDData) into Postgres.
- **Auth revalidation:** `PosAuthStateProvider` captures the principal at circuit
  start; consider DB revalidation for long-lived sessions / disabled users.
- **Phase 17 — impersonation. DONE, audit layer closed by Phase 18.** A platform
  admin can "log in as" any user in any business from `/admin/imitim/{id}`: the
  session becomes a full business principal (every gate and tenant query sees exactly
  what that user sees) stamped with `impersonator*` claims, a persistent amber banner
  names who is really driving, and "Kthehu te platforma" restores the operator with
  no second login. Every impersonated act is now recorded in both the control and the
  business database, and `Receipt.ImpersonatedBy` turned out to be unbuildable as
  specified — see Phase 18.
- **Startup migration sweep** is serial and blocking; move to a background service
  once the business count grows.
- **Audit retention.** The log is append-only and nothing prunes it. A busy shop
  writes a row per sale, so plan a retention policy (or a partition) before it is the
  biggest table in the database.
- **Read-only DB console** inside the POS (decided against a generic row editor;
  CloudBeaver/pgAdmin behind the tunnel covers the operator case).
