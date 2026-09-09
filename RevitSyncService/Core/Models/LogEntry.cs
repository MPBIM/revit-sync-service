using System;

namespace RevitSyncService.Core.Models
{
    public enum LogLevel
    {
        Info,
        Success,
        Warning,
        Error
    }

    public class LogEntry
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public LogLevel Level { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? ProjectName { get; set; }
        public string? Details { get; set; }

        public string Display => $"[{Timestamp:yyyy-MM-dd HH:mm:ss}] [{Level}] {(ProjectName != null ? $"[{ProjectName}] " : "")}{Message}";
    }
}
