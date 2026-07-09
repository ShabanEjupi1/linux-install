# KosovaPOS Agent

A tiny local HTTP service that runs on each **cashier PC**, next to the fiscal /
receipt / barcode printers and the scale. The Blazor POS runs on a server, but the
**browser** runs on the cashier PC — the same machine as the hardware — so the
browser (`wwwroot/js/hardware.js`) fetches this agent at `http://127.0.0.1:9099`.

That means the POS server never needs an inbound path to the NAT'd cashier PC, and
browsers treat `http://127.0.0.1` as a secure context, so an HTTPS POS page can call
it without mixed-content errors.

```
browser (cashier PC)  ──fetch──▶  KosovaPOS.Agent :9099  ──▶  F-Link / printers / COM scale
        ▲                                                          │
        └── Blazor circuit ── server builds the fiscal payload ────┘  (FiscalReceiptBuilder in Core)
```

## Drivers

- **Windows** → real drivers:
  - **Fiscal**: writes `Fatura.inp` into F-Link's watched folder and polls for
    consume/`.err` (F-Link owns the COM port and talks to the fiscal device).
  - **Receipt**: ESC/POS raw text via the Win32 spooler (`winspool.drv`).
  - **Barcode**: TSPL label via the same raw spooler path.
  - **Scale**: serial (`System.IO.Ports`), parses the first stable weight line.
- **Anything else (or `AGENT_MOCK=true`)** → mock drivers that log and (for fiscal)
  drop the payload into a temp folder. Lets the whole pipeline be tested off Windows.

## Run

```bash
dotnet run --project src/KosovaPOS.Agent           # mock on Linux/macOS, real on Windows
AGENT_MOCK=true dotnet run --project src/KosovaPOS.Agent   # force mock anywhere
```

Endpoints: `GET /health`, `POST /fiscal/print`, `POST /receipt/print`,
`POST /barcode/print`, `GET /scale/read`. Bound to loopback only.

## Configuration (environment variables)

Same names the desktop app used, so an existing cashier PC keeps working.

| Var | Default | Meaning |
|-----|---------|---------|
| `AGENT_PORT` | `9099` | Loopback port the browser fetches |
| `AGENT_MOCK` | `false` | Force mock drivers even on Windows |
| `AGENT_ALLOWED_ORIGINS` | `*` | CSV of allowed origins (set to the POS URL in prod) |
| `FISCAL_TEMP_PATH` | `C:\TEMP\` | F-Link watched folder |
| `FISCAL_PRINTER_PORT` | `COM1` | Fiscal device COM (advisory; F-Link owns it) |
| `FISCAL_PRINTER_MODEL` | `FP700+` | Shown in `/health` |
| `FISCAL_NUMBER` | `003910` | Shown in `/health` |
| `RECEIPT_PRINTER` | *(default printer)* | Windows printer name for courtesy receipts |
| `BARCODE_PRINTER` | *(none)* | Windows printer name for labels |
| `SCALE_PORT` / `SCALE_BAUD` | `COM3` / `9600` | Serial scale |
| `AGENT_LOG_DIR` | `%ProgramData%\KosovaPOS\Agent\logs` | Rolling daily log files (14 days) |

On a shop PC these are set for you by `install-agent.ps1`, scoped to the service's
registry key rather than the machine environment.

## Deploy on a Windows cashier PC

Build the package (works from Linux — no Windows box needed):

```bash
./tools/publish-agent.sh      # -> publish/agent/KosovaPOS-Agent-<version>.zip
```

Copy the zip to the cashier PC, unzip, and from an **elevated** PowerShell:

```powershell
.\install-agent.ps1 -Mock     # plumbing check, touches no hardware
.\install-agent.ps1 -ReceiptPrinter "POS-80"   # the real thing
.\test-agent.ps1 -Fiscal      # prints a 0.01 EUR fiscal test receipt
```

The installer registers the `KosovaPOSAgent` service (auto-start, restart on
crash), writes the config, starts it and verifies `/health`. Re-running upgrades
in place. Full instructions, incl. troubleshooting, in
[`deploy/INSTALL.md`](deploy/INSTALL.md).

Because it runs as a service the agent has no console, so it writes rolling logs
to `%ProgramData%\KosovaPOS\Agent\logs`; `/health` reports the exact path.
