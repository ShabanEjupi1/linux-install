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

## Deploy on a Windows cashier PC

1. Publish a self-contained single file:
   ```bash
   dotnet publish src/KosovaPOS.Agent -c Release -r win-x64 --self-contained \
     -p:PublishSingleFile=true -o publish-agent
   ```
2. Copy `publish-agent\KosovaPOS.Agent.exe` to the PC.
3. Set the env vars above (system-wide) and register it as a service or a
   Startup task so it launches with Windows (e.g. `sc create KosovaPOSAgent binPath=…`
   or `nssm`). Verify with `curl http://127.0.0.1:9099/health`.
4. In production set `AGENT_ALLOWED_ORIGINS=https://pos.spacecode.tech`.
