using System;

namespace RevitSyncService.Core.Models
{
    public enum ClashTaskStatus
    {
        Pending,
        Running,
        Done,
        Failed
    }

    public class ClashTask
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ProjectId { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;

        /// <summary>
        /// Папка с NWC файлами — Navisworks-приложение возьмёт их отсюда
        /// </summary>
        public string NwcFolder { get; set; } = string.Empty;

        public ClashTaskStatus Status { get; set; } = ClashTaskStatus.Pending;
        public string? ErrorMessage { get; set; }
        public string? RevitVersion { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}