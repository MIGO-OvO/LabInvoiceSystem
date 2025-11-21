using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using LabInvoiceSystem.Models;

namespace LabInvoiceSystem.Services
{
    public class LoggerService
    {
        private readonly string _logFilePath;
        private List<LogEntry> _logs;

        public LoggerService()
        {
            var appDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "LabInvoiceSystem"
            );

            if (!Directory.Exists(appDataDir))
            {
                Directory.CreateDirectory(appDataDir);
            }

            _logFilePath = Path.Combine(appDataDir, "upload_logs.json");
            _logs = LoadLogs();
        }

        public void LogUpload(string fileName, InvoiceInfo info)
        {
            AddEntry("upload", $"上传文件: {fileName}, 金额: {info.Amount}元");
        }

        public void LogArchive(string fileName)
        {
            AddEntry("archive", $"归档文件: {fileName}");
        }

        public void LogDelete(string details)
        {
            AddEntry("delete", details);
        }

        public void LogExport(string details)
        {
            AddEntry("export", details);
        }

        public List<LogEntry> GetLogs()
        {
            return new List<LogEntry>(_logs);
        }

        public void ClearLogs()
        {
            _logs.Clear();
            SaveLogs();
        }

        private List<LogEntry> LoadLogs()
        {
            try
            {
                if (File.Exists(_logFilePath))
                {
                    var json = File.ReadAllText(_logFilePath);
                    return JsonSerializer.Deserialize<List<LogEntry>>(json) ?? new List<LogEntry>();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载日志失败: {ex.Message}");
            }

            return new List<LogEntry>();
        }

        private void AddEntry(string action, string details)
        {
            var entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Action = action,
                Details = details
            };

            _logs.Insert(0, entry);
            SaveLogs();
        }

        private void SaveLogs()
        {
            try
            {
                var json = JsonSerializer.Serialize(_logs, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(_logFilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"保存日志失败: {ex.Message}");
            }
        }
    }
}
