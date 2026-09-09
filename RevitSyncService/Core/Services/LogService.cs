using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using RevitSyncService.Core.Interfaces;
using RevitSyncService.Core.Models;

namespace RevitSyncService.Core.Services
{
    public class LogService : ILogService
    {
        private readonly List<LogEntry> _entries = new();
        private readonly object _lock = new();
        private string _logFolder;
        private int _retentionDays;

        public event Action<LogEntry>? OnNewEntry;

        public LogService(string logFolder, int retentionDays = 30)
        {
            _logFolder = Environment.ExpandEnvironmentVariables(logFolder);
            _retentionDays = retentionDays;
            Directory.CreateDirectory(_logFolder);
            CleanupOldLogs();
        }

        public void Log(LogLevel level, string message, string? projectName = null, string? details = null)
        {
            var entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = level,
                Message = message,
                ProjectName = projectName,
                Details = details
            };

            lock (_lock)
            {
                _entries.Add(entry);
            }

            // Запись в файл
            WriteToFile(entry);

            // Уведомление UI
            OnNewEntry?.Invoke(entry);
        }

        public void Info(string message, string? projectName = null) =>
            Log(LogLevel.Info, message, projectName);

        public void Success(string message, string? projectName = null) =>
            Log(LogLevel.Success, message, projectName);

        public void Warning(string message, string? projectName = null) =>
            Log(LogLevel.Warning, message, projectName);

        public void Error(string message, string? projectName = null, string? details = null) =>
            Log(LogLevel.Error, message, projectName, details);

        public IReadOnlyList<LogEntry> GetEntries()
        {
            lock (_lock)
            {
                return _entries.ToList().AsReadOnly();
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _entries.Clear();
            }
        }

        private void WriteToFile(LogEntry entry)
        {
            try
            {
                string fileName = $"log_{DateTime.Now:yyyy-MM-dd}.txt";
                string filePath = Path.Combine(_logFolder, fileName);
                string line = $"{entry.Display}{(entry.Details != null ? $"\n  Details: {entry.Details}" : "")}\n";
                File.AppendAllText(filePath, line);
            }
            catch { /* Не падаем при ошибке логирования */ }
        }

        private void CleanupOldLogs()
        {
            try
            {
                var cutoff = DateTime.Now.AddDays(-_retentionDays);
                foreach (var file in Directory.GetFiles(_logFolder, "log_*.txt"))
                {
                    if (File.GetCreationTime(file) < cutoff)
                        File.Delete(file);
                }
            }
            catch { }
        }
    }
}
