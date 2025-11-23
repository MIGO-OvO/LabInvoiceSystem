using System;
using System.IO;
using System.Text.Json;
using LabInvoiceSystem.Models;

namespace LabInvoiceSystem.Services
{
    public class SettingsService
    {
        private static SettingsService? _instance;
        private static readonly object _lock = new object();
        
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
        
        public void SaveSettings()
        {
            try
            {
                var directory = Path.GetDirectoryName(_settingsFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                File.WriteAllText(_settingsFilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"保存配置失败: {ex.Message}");
            }
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
