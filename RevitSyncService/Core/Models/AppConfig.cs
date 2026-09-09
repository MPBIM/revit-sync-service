using System.Collections.Generic;

namespace RevitSyncService.Core.Models
{
    public class AppConfig
    {
        public string Version { get; set; } = "1.0";
        public GlobalSettings GlobalSettings { get; set; } = new();
        public List<Project> Projects { get; set; } = new();
    }

    public class GlobalSettings
    {
        public bool AutoExportEnabled { get; set; } = true;
        public List<string> ExcludedFolders { get; set; } = new() { "Прочее", "Archive", "Backup" };
        public string TempFolder { get; set; } = "%AppData%\\RevitSyncService\\Temp";
        public string LogFolder { get; set; } = "%AppData%\\RevitSyncService\\Logs";
        public int LogRetentionDays { get; set; } = 30;
    }
}
