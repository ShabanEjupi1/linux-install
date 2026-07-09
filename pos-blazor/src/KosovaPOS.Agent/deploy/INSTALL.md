# Installing the KosovaPOS Hardware Agent on the shop PC

Without this agent the web POS **cannot print fiscal receipts**, which means it
cannot legally sell. The desktop app used to do this job; the agent replaces it.

Everything runs on the cashier PC. The browser on that PC fetches
`http://127.0.0.1:9099`, so the POS server never needs a way in through NAT.

## Before you start

1. **Windows PC** where the fiscal printer, receipt printer and scale are attached.
2. **F-Link installed and licensed.** F-Link is the Kosovo fiscal middleware; it
   owns the COM port and talks to the tax device. The agent only drops a
   `Fatura.inp` file into F-Link's watched folder. Apply licence key
   `BMC-4F73-6A2B-10D9` before expecting real receipts.
3. Note the folder F-Link watches (usually `C:\TEMP\`) and the exact Windows
   printer names (**Settings → Printers & scanners**).

No .NET runtime is needed — the exe is self-contained.

## Install

Copy `KosovaPOS-Agent-<version>.zip` to the PC and unzip it. Then open PowerShell
**as administrator**, `cd` into the unzipped folder, and run:

```powershell
# Recommended first: mock mode. Touches no hardware, proves the plumbing works.
.\install-agent.ps1 -Mock
```

Open <https://pos.spacecode.tech> **on this PC**, go to *Shitje*, and check the
agent badge — amber means the agent is reachable and running mocks.

Then install for real:

```powershell
.\install-agent.ps1 `
    -FiscalTempPath "C:\TEMP\" `
    -FiscalComPort  "COM1" `
    -FiscalNumber   "003910" `
    -ReceiptPrinter "POS-80" `
    -BarcodePrinter "TSC-244" `
    -ScalePort      "COM3"
```

Re-running the script upgrades in place. The badge should now be **green**.

Every parameter is optional and falls back to the defaults the desktop app used.
`-ReceiptPrinter` empty means "the Windows default printer".

## Verify

```powershell
.\test-agent.ps1            # health + courtesy receipt + scale, no fiscal
.\test-agent.ps1 -Fiscal    # ALSO prints a real 0.01 EUR fiscal test receipt
```

`-Fiscal` prompts before sending anything to the tax device. Run it when the shop
is closed.

## Where things live

| What | Where |
|------|-------|
| Service | `KosovaPOSAgent` (starts automatically, restarts on crash) |
| Binary | `C:\Program Files\KosovaPOS Agent\KosovaPOS.Agent.exe` |
| Logs | `C:\ProgramData\KosovaPOS\Agent\logs\agent-YYYYMMDD.log` (14 days) |
| Settings | service-scoped env vars, under the service's registry key |
| Failed fiscal files | `<FiscalTempPath>\PrintErrors\` |

## Troubleshooting

**Badge is grey (agent offline).** The service isn't running or isn't reachable:

```powershell
Get-Service KosovaPOSAgent
Start-Service KosovaPOSAgent
Invoke-RestMethod http://127.0.0.1:9099/health
```

**Badge is amber but you installed without `-Mock`.** Check `/health` →
`realHardware`. The agent only uses real drivers on Windows; confirm no stale
`AGENT_MOCK=true` survives from a mock install by re-running `install-agent.ps1`
without `-Mock`.

**Fiscal print times out.** The agent wrote `Fatura.inp` but F-Link never consumed
it. Confirm F-Link is running, licensed, and watching the same folder the agent
reports in `/health` → `fiscal.tempPath`. The unconsumed file is moved to
`PrintErrors\` with a timestamp.

**A sale succeeded but nothing printed.** That is by design — a saved sale is never
rolled back because a printer failed. Reprint from *Faturat*; the sale is in the DB.

**Receipt printer works for the user but not the service.** The service runs as
LocalSystem, which sees system-wide printers but not per-user printer connections.
Reinstall the printer for all users, or install the agent under the cashier account
(`sc.exe config KosovaPOSAgent obj= ".\cashier" password= "..."`).

## Uninstall

```powershell
.\uninstall-agent.ps1           # remove the service, keep logs
.\uninstall-agent.ps1 -Purge    # also delete the binary and logs
```
