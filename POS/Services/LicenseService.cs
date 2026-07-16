using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Diagnostics;

namespace KosovaPOS.Services
{
    public class LicenseData
    {
        public string MachineId { get; set; } = string.Empty;
        public string LicenseKey { get; set; } = string.Empty;
        public DateTime ExpirationDate { get; set; }
        public DateTime IssuedDate { get; set; }
        public string CustomerName { get; set; } = string.Empty;
    }

    public class LicenseService
    {
        private static readonly string LicenseFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramData),
            "KosovaPOS",
            "license.lic"
        );

        private static readonly string LicenseKeyPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramData),
            "KosovaPOS",
            ".key"
        );

        public static string GetMachineId()
        {
            try
            {
                // Get unique machine identifiers using WMI
                var machineId = GetHardwareId();
                var hashed = HashMachineId(machineId);
                return hashed;
            }
            catch
            {
                return "UNKNOWN_HARDWARE";
            }
        }

        private static string GetHardwareId()
        {
            try
            {
                // Get CPU ID, Motherboard ID, and HDD Serial
                var cpuId = GetWmiProperty("Win32_Processor", "ProcessorId");
                var mbId = GetWmiProperty("Win32_BaseBoard", "SerialNumber");
                var diskId = GetWmiProperty("Win32_LogicalDisk", "VolumeSerialNumber");

                return $"{cpuId}|{mbId}|{diskId}";
            }
            catch
            {
                // Fallback: use machine name and username
                return $"{Environment.MachineName}|{Environment.UserName}";
            }
        }

        private static string GetWmiProperty(string wmiClass, string wmiProperty)
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "wmic",
                        Arguments = $"path {wmiClass} get {wmiProperty} /format:value",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                var value = output.Split('=')[^1].Trim();
                return string.IsNullOrEmpty(value) ? "NA" : value;
            }
            catch
            {
                return "NA";
            }
        }

        private static string HashMachineId(string machineId)
        {
            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(machineId));
                return Convert.ToBase64String(hash).Substring(0, 16);
            }
        }

        public static bool ValidateLicense()
        {
            try
            {
                if (!File.Exists(LicenseFilePath))
                {
                    return false;
                }

                var licenseJson = File.ReadAllText(LicenseFilePath);
                var licenseData = JsonSerializer.Deserialize<LicenseData>(licenseJson);

                if (licenseData == null)
                {
                    return false;
                }

                // Verify machine ID matches
                var currentMachineId = GetMachineId();
                if (licenseData.MachineId != currentMachineId)
                {
                    return false;
                }

                // Verify expiration date
                if (DateTime.Now > licenseData.ExpirationDate)
                {
                    return false;
                }

                // Verify license key signature
                if (!VerifyLicenseKey(licenseData))
                {
                    return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool VerifyLicenseKey(LicenseData licenseData)
        {
            try
            {
                var keyData = $"{licenseData.MachineId}|{licenseData.ExpirationDate:yyyyMMdd}|{licenseData.CustomerName}";
                var expectedKey = GenerateLicenseKey(keyData);
                return licenseData.LicenseKey == expectedKey;
            }
            catch
            {
                return false;
            }
        }

        public static string GenerateLicenseKey(string data)
        {
            try
            {
                using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes("KosovaPOS_SECRET_2024")))
                {
                    var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
                    return Convert.ToBase64String(hash).Substring(0, 32);
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        public static bool ActivateLicense(string licenseKey, string customerName)
        {
            try
            {
                var machineId = GetMachineId();
                var expirationDate = DateTime.Now.AddYears(1);
                var issuedDate = DateTime.Now;

                var keyData = $"{machineId}|{expirationDate:yyyyMMdd}|{customerName}";
                var generatedKey = GenerateLicenseKey(keyData);

                if (licenseKey != generatedKey)
                {
                    return false;
                }

                var licenseData = new LicenseData
                {
                    MachineId = machineId,
                    LicenseKey = licenseKey,
                    ExpirationDate = expirationDate,
                    IssuedDate = issuedDate,
                    CustomerName = customerName
                };

                Directory.CreateDirectory(Path.GetDirectoryName(LicenseFilePath) ?? "");
                var json = JsonSerializer.Serialize(licenseData, new JsonSerializerOptions { WriteIndented = true });
                
                // Write with restricted permissions
                File.WriteAllText(LicenseFilePath, json);
                SetFilePermissions(LicenseFilePath);

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void SetFilePermissions(string filePath)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                var fileSecurity = fileInfo.GetAccessControl();
                
                // Allow only SYSTEM and current user to read
                fileSecurity.SetAccessRuleProtection(true, false);
                fileInfo.SetAccessControl(fileSecurity);
            }
            catch
            {
                // If permissions fail, continue anyway
            }
        }

        public static int GetDaysUntilExpiration()
        {
            try
            {
                if (!File.Exists(LicenseFilePath))
                {
                    return 0;
                }

                var licenseJson = File.ReadAllText(LicenseFilePath);
                var licenseData = JsonSerializer.Deserialize<LicenseData>(licenseJson);

                if (licenseData == null)
                {
                    return 0;
                }

                var daysRemaining = (int)(licenseData.ExpirationDate - DateTime.Now).TotalDays;
                return daysRemaining > 0 ? daysRemaining : 0;
            }
            catch
            {
                return 0;
            }
        }
    }
}
