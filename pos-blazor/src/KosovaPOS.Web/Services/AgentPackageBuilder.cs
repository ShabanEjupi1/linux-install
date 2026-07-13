using System.IO.Compression;
using System.Text;
using KosovaPOS.Models;

namespace KosovaPOS.Web.Services;

/// <summary>
/// Repacks the generic agent zip as THIS shop's install package.
///
/// The plain download is the same file for every business, so whoever unzipped it on the shop PC
/// had to know things that only the POS knows — which server to trust, which printer prints the
/// receipts, which one prints A4 — and type them into a command line. That is exactly the step
/// that gets skipped, and a skipped step here is silent: the agent installs, reports healthy, and
/// prints every invoice to the wrong printer.
///
/// So the package that comes off /pajisjet carries the answers with it: a konfigurimi.env the
/// installer reads, a one-click instalo-ketu.cmd, and a LEXOME.txt in Albanian. Nothing to edit.
/// </summary>
public sealed class AgentPackageBuilder
{
    private readonly AgentPackage _package;

    public AgentPackageBuilder(AgentPackage package) => _package = package;

    /// <summary>
    /// The shop's package, as zip bytes. Built per request (a few MB of copying) rather than
    /// cached: it is downloaded once per shop PC, and a cached copy would go stale the moment
    /// the shop changed a printer — which is the one thing it exists to carry.
    /// </summary>
    public byte[] Build(BusinessSettings? shop, string posOrigin)
    {
        if (!_package.Available || _package.ZipPath is null)
            throw new InvalidOperationException("Agent package is not staged on this server.");

        using var output = new MemoryStream();

        // Copied entry by entry, not extracted to disk: the agent exe is ~100MB unpacked and this
        // runs on a 1-core VM.
        using (var source = ZipFile.OpenRead(_package.ZipPath))
        using (var target = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            string? root = null;

            foreach (var entry in source.Entries)
            {
                root ??= entry.FullName.Split('/', '\\').FirstOrDefault();

                var copy = target.CreateEntry(entry.FullName, CompressionLevel.Optimal);
                using var from = entry.Open();
                using var to = copy.Open();
                from.CopyTo(to);
            }

            // The folder the base zip nests everything in (KosovaPOS-Agent-1.2.0). Read from the
            // zip rather than assembled from the version, so a renamed package still lands in it.
            var prefix = string.IsNullOrEmpty(root) ? "" : root + "/";

            Add(target, prefix + "konfigurimi.env", ConfigFile(shop, posOrigin));
            Add(target, prefix + "LEXOME.txt", Readme(shop, posOrigin));
            Add(target, prefix + "instalo-ketu.cmd", InstallCmd());
        }

        return output.ToArray();
    }

    private static void Add(ZipArchive zip, string name, string content)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var stream = entry.Open();
        // CRLF and a BOM: these are read by Notepad and by cmd.exe on Windows, and a Unix-line-ended
        // .cmd file runs its last line without a terminator.
        var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)
            .GetBytes(content.ReplaceLineEndings("\r\n"));
        stream.Write(bytes, 0, bytes.Length);
    }

    /// <summary>
    /// What install-agent.ps1 reads instead of being given arguments. Only settings the POS
    /// actually knows are written; the fiscal transport is the technician's, and appears here
    /// commented out rather than guessed at — a wrong FISCAL_NUMBER prints wrong tax receipts.
    /// </summary>
    private static string ConfigFile(BusinessSettings? shop, string posOrigin)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# KosovaPOS — konfigurimi i agjentit për këtë biznes.");
        sb.AppendLine("# E lexon vetë install-agent.ps1 (dhe instalo-ketu.cmd). Nuk ka nevojë të ndryshohet.");
        sb.AppendLine($"# Gjeneruar: {DateTime.Now:dd.MM.yyyy HH:mm}");
        sb.AppendLine();
        sb.AppendLine("# POS-i i këtij biznesi. Vetëm kjo adresë lejohet ta thërrasë agjentin.");
        sb.AppendLine($"POS_URL={posOrigin}");
        sb.AppendLine();
        sb.AppendLine("# Printerët, ashtu si u zgjodhën te Pajisjet. Bosh = printeri i parazgjedhur i Windows-it.");
        sb.AppendLine("# Këta janë vetëm rezerva: POS-i ia dërgon emrin e printerit çdo printimi, prandaj");
        sb.AppendLine("# ndryshimi te Pajisjet zbatohet menjëherë, pa e riinstaluar agjentin.");
        sb.AppendLine($"RECEIPT_PRINTER={shop?.ReceiptPrinter}");
        sb.AppendLine($"BARCODE_PRINTER={shop?.BarcodePrinter}");
        sb.AppendLine($"INVOICE_PRINTER={shop?.InvoicePrinter}");
        sb.AppendLine($"RECEIPT_CODEPAGE={(string.IsNullOrWhiteSpace(shop?.ReceiptCodePage) ? "1252" : shop!.ReceiptCodePage)}");
        sb.AppendLine();
        sb.AppendLine("# Printeri fiskal (F-Link). Këto i cakton tekniku i pajisjes, jo POS-i —");
        sb.AppendLine("# hiqi '#' dhe plotësoji vetëm nëse dallojnë nga vlerat e mëposhtme.");
        sb.AppendLine("# FISCAL_TEMP_PATH=C:\\TEMP\\");
        sb.AppendLine("# FISCAL_PRINTER_PORT=COM1");
        sb.AppendLine("# FISCAL_NUMBER=003910");
        return sb.ToString();
    }

    private static string Readme(BusinessSettings? shop, string posOrigin)
    {
        static string Or(string? value, string fallback) =>
            string.IsNullOrWhiteSpace(value) ? fallback : value!;

        var sb = new StringBuilder();
        sb.AppendLine("KosovaPOS — agjenti i printimit");
        sb.AppendLine("================================");
        sb.AppendLine();
        sb.AppendLine($"Biznesi : {Or(shop?.BusinessName, "KosovaPOS")}");
        sb.AppendLine($"POS-i   : {posOrigin}");
        sb.AppendLine();
        sb.AppendLine("Kjo paketë është ndërtuar PËR KËTË BIZNES. Konfigurimi është brenda saj");
        sb.AppendLine("(konfigurimi.env) — nuk ka çka të redaktohet dhe nuk ka argumente për t'u shkruar.");
        sb.AppendLine();
        sb.AppendLine("INSTALIMI (në kompjuterin e arkës, ku janë printerët):");
        sb.AppendLine();
        sb.AppendLine("  1. Shpaketo TË GJITHË dosjen (mos e nis nga brenda zip-it).");
        sb.AppendLine("  2. Kliko me të djathtën mbi 'instalo-ketu.cmd' -> Run as administrator.");
        sb.AppendLine("  3. Prit derisa të shkruajë 'KosovaPOS Agent is running'.");
        sb.AppendLine($"  4. Hap {posOrigin} në KËTË kompjuter. Te Pajisjet, shenja duhet të jetë 'Pajisje'.");
        sb.AppendLine();
        sb.AppendLine("PRINTERËT E ZGJEDHUR TANI:");
        sb.AppendLine();
        sb.AppendLine($"  Kuponi 80mm : {Or(shop?.ReceiptPrinter, "(i parazgjedhuri i Windows-it)")}");
        sb.AppendLine($"  Etiketat    : {Or(shop?.BarcodePrinter, "(i parazgjedhuri i Windows-it)")}");
        sb.AppendLine($"  Fatura A4   : {Or(shop?.InvoicePrinter, "(i parazgjedhuri i Windows-it)")}");
        sb.AppendLine();
        sb.AppendLine("Këta ndryshohen nga POS-i (Pajisjet ose Cilësimet) dhe zbatohen menjëherë —");
        sb.AppendLine("agjenti NUK riinstalohet për të ndërruar një printer.");
        sb.AppendLine();
        sb.AppendLine("PSE DUHET AGJENTI:");
        sb.AppendLine();
        sb.AppendLine("  Një faqe interneti nuk e zgjedh dot printerin. Shfletuesi printon aty ku");
        sb.AppendLine("  ia thotë dialogu i printimit, dhe kur dialogu hiqet (--kiosk-printing) printon");
        sb.AppendLine("  GJITHMONË te printeri i parazgjedhur i Windows-it. Në arkë ai është rrotulla");
        sb.AppendLine("  termike — pra fatura A4 do të dilte si një metër letër termike. Agjenti është");
        sb.AppendLine("  i vetmi që mund t'i dërgojë kuponin rrotullës, etiketën HPRT-së dhe faturën A4-shit.");
        sb.AppendLine();
        sb.AppendLine("ÇINSTALIMI:  kliko me të djathtën mbi 'uninstall-agent.ps1' -> Run with PowerShell (si administrator).");
        sb.AppendLine("REGJISTRAT :  C:\\ProgramData\\KosovaPOS\\Agent\\logs");
        return sb.ToString();
    }

    /// <summary>
    /// Right-click → Run as administrator, and it is done. The .ps1 is what does the work, but a
    /// .ps1 cannot be run that way from Explorer, and the shop PC's execution policy blocks it
    /// anyway — which is how "just run the script" turns into a support call.
    /// </summary>
    private static string InstallCmd() => """
        @echo off
        setlocal
        cd /d "%~dp0"

        net session >nul 2>&1
        if errorlevel 1 (
            echo.
            echo   Kliko me te djathten mbi kete skedar -^> "Run as administrator".
            echo.
            pause
            exit /b 1
        )

        rem PowerShell refuses a script downloaded from the internet (Zone.Identifier), and the
        rem shop PC's execution policy refuses it twice. Both are cleared for THIS folder only.
        powershell -NoProfile -ExecutionPolicy Bypass -Command "Get-ChildItem -Path '%~dp0*.ps1' | Unblock-File; & '%~dp0install-agent.ps1'"

        echo.
        pause
        """;
}
