using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using LabInvoiceSystem.Models;

namespace LabInvoiceSystem.Services
{
    public class SettingsService
    {
        private static SettingsService? _instance;
        private static readonly object _lock = new object();

#if DEBUG
        static SettingsService()
        {
            var corrections = new Dictionary<string, string>();
            if (!UpdateItemNameCorrection(corrections, "seller-1", "", "1", "电线电缆") ||
                GetItemNameCorrection(corrections, "seller-1", "", "1") != "电线电缆" ||
                GetItemNameCorrection(corrections, "seller-2", "", "1") != null ||
                !UpdateItemNameCorrection(corrections, "seller-1", "", "1", "1") ||
                GetItemNameCorrection(corrections, "seller-1", "", "1") != null)
            {
                throw new InvalidOperationException("Item name learning self-check failed.");
            }
        }
#endif
        
        private readonly string _settingsFilePath;
        public AppSettings Settings { get; private set; }
        
        private SettingsService()
        {
            _settingsFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "LabInvoiceSystem",
                "appsettings.json"
            );
            
            Settings = LoadSettings();

            var environmentSecret = Environment.GetEnvironmentVariable("LABINVOICESYSTEM_BAIDU_SECRET_KEY");
            if (!string.IsNullOrWhiteSpace(environmentSecret))
            {
                Settings.BaiduSecretKey = environmentSecret;
            }
            else if (!string.IsNullOrWhiteSpace(Settings.BaiduSecretKeyEncrypted))
            {
                try
                {
                    Settings.BaiduSecretKey = UnprotectSecret(Settings.BaiduSecretKeyEncrypted);
                }
                catch (Exception ex)
                {
                    Settings.BaiduSecretKey = string.Empty;
                    Console.WriteLine($"读取加密 Secret Key 失败: {ex.Message}");
                }
            }
            else if (!string.IsNullOrWhiteSpace(Settings.BaiduSecretKey))
            {
                SaveSettings(); // 迁移旧版明文配置。
            }
        }
        
        public static SettingsService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new SettingsService();
                    }
                }
                return _instance;
            }
        }
        
        private AppSettings LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    var json = File.ReadAllText(_settingsFilePath);
                    return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载配置失败: {ex.Message}");
            }
            
            return new AppSettings();
        }
        
        public bool SaveSettings()
        {
            var secretKey = Settings.BaiduSecretKey;
            string? tempPath = null;
            try
            {
                var directory = Path.GetDirectoryName(_settingsFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                if (!string.IsNullOrWhiteSpace(secretKey) && OperatingSystem.IsWindows())
                {
                    Settings.BaiduSecretKeyEncrypted = ProtectSecret(secretKey);
                }
                else if (!OperatingSystem.IsWindows())
                {
                    Settings.BaiduSecretKeyEncrypted = string.Empty;
                }

                // 运行时保留明文；Windows 写 DPAPI 密文，其他平台不持久化 Secret Key。
                Settings.BaiduSecretKey = string.Empty;
                var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions
                { 
                    WriteIndented = true 
                });
                tempPath = _settingsFilePath + ".tmp";
                File.WriteAllText(tempPath, json);
                File.Move(tempPath, _settingsFilePath, true);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"保存配置失败: {ex.Message}");
                return false;
            }
            finally
            {
                Settings.BaiduSecretKey = secretKey;
                if (!string.IsNullOrEmpty(tempPath) && File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); } catch { }
                }
            }
        }

        private static string ProtectSecret(string value)
        {
            if (!OperatingSystem.IsWindows())
            {
                throw new PlatformNotSupportedException("Secret Key 加密目前仅支持 Windows。");
            }

            return Convert.ToBase64String(ProtectData(Encoding.UTF8.GetBytes(value)));
        }

        private static string UnprotectSecret(string value)
        {
            if (!OperatingSystem.IsWindows())
            {
                throw new PlatformNotSupportedException("Secret Key 解密目前仅支持 Windows。");
            }

            return Encoding.UTF8.GetString(UnprotectData(Convert.FromBase64String(value)));
        }

        private static byte[] ProtectData(byte[] data)
        {
            return TransformData(data, true);
        }

        private static byte[] UnprotectData(byte[] data)
        {
            return TransformData(data, false);
        }

        private static byte[] TransformData(byte[] data, bool protect)
        {
            var input = new DataBlob
            {
                Length = data.Length,
                Data = Marshal.AllocHGlobal(data.Length)
            };
            Marshal.Copy(data, 0, input.Data, data.Length);

            IntPtr description = IntPtr.Zero;
            try
            {
                DataBlob output;
                var succeeded = protect
                    ? CryptProtectData(ref input, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero,
                        CryptProtectUiForbidden, out output)
                    : CryptUnprotectData(ref input, out description, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero,
                        CryptProtectUiForbidden, out output);

                if (!succeeded)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                try
                {
                    var result = new byte[output.Length];
                    Marshal.Copy(output.Data, result, 0, output.Length);
                    return result;
                }
                finally
                {
                    LocalFree(output.Data);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(input.Data);
                if (description != IntPtr.Zero)
                {
                    LocalFree(description);
                }
            }
        }

        private const int CryptProtectUiForbidden = 0x1;

        [StructLayout(LayoutKind.Sequential)]
        private struct DataBlob
        {
            public int Length;
            public IntPtr Data;
        }

        [DllImport("Crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CryptProtectData(ref DataBlob dataIn, string? description,
            IntPtr optionalEntropy, IntPtr reserved, IntPtr prompt, int flags, out DataBlob dataOut);

        [DllImport("Crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CryptUnprotectData(ref DataBlob dataIn, out IntPtr description,
            IntPtr optionalEntropy, IntPtr reserved, IntPtr prompt, int flags, out DataBlob dataOut);

        [DllImport("Kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr memory);

        public string ApplyItemNameCorrection(string? sellerTaxId, string? sellerName, string? ocrItemName)
        {
            return GetItemNameCorrection(Settings.ItemNameCorrections, sellerTaxId, sellerName, ocrItemName)
                   ?? ocrItemName ?? string.Empty;
        }

        public void RememberItemNameCorrection(string? sellerTaxId, string? sellerName,
            string? ocrItemName, string? correctedItemName)
        {
            Settings.ItemNameCorrections ??= new Dictionary<string, string>();
            if (!UpdateItemNameCorrection(Settings.ItemNameCorrections,
                    sellerTaxId, sellerName, ocrItemName, correctedItemName))
            {
                return;
            }

            SaveSettings();
        }

        private static bool UpdateItemNameCorrection(Dictionary<string, string> corrections,
            string? sellerTaxId, string? sellerName, string? ocrItemName, string? correctedItemName)
        {
            var key = BuildCorrectionKey(sellerTaxId, sellerName, ocrItemName);
            var correction = correctedItemName?.Trim() ?? string.Empty;
            if (key == null || string.IsNullOrEmpty(correction))
            {
                return false;
            }

            if (string.Equals(ocrItemName?.Trim(), correction, StringComparison.Ordinal))
            {
                return corrections.Remove(key);
            }

            if (corrections.TryGetValue(key, out var existing) && existing == correction)
            {
                return false;
            }

            corrections[key] = correction;
            return true;
        }

        private static string? GetItemNameCorrection(Dictionary<string, string>? corrections,
            string? sellerTaxId, string? sellerName, string? ocrItemName)
        {
            var key = BuildCorrectionKey(sellerTaxId, sellerName, ocrItemName);
            return key != null && corrections != null && corrections.TryGetValue(key, out var correction)
                ? correction
                : null;
        }

        private static string? BuildCorrectionKey(string? sellerTaxId, string? sellerName, string? ocrItemName)
        {
            var seller = string.IsNullOrWhiteSpace(sellerTaxId)
                ? sellerName?.Trim() ?? string.Empty
                : sellerTaxId.Trim();
            var item = ocrItemName?.Trim() ?? string.Empty;
            return string.IsNullOrEmpty(seller) || string.IsNullOrEmpty(item)
                ? null
                : $"{seller.ToUpperInvariant()}\u001F{item.ToUpperInvariant()}";
        }
        
        public void EnsureDirectoriesExist()
        {
            try
            {
                if (!Directory.Exists(Settings.ArchiveDirectory))
                {
                    Directory.CreateDirectory(Settings.ArchiveDirectory);
                }
                
                if (!Directory.Exists(Settings.TempUploadDirectory))
                {
                    Directory.CreateDirectory(Settings.TempUploadDirectory);
                }

                if (!string.IsNullOrWhiteSpace(Settings.ExportDirectory) &&
                    !Directory.Exists(Settings.ExportDirectory))
                {
                    Directory.CreateDirectory(Settings.ExportDirectory);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"创建目录失败: {ex.Message}");
            }
        }
    }
}
