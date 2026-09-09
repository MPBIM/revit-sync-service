using System.Collections.Generic;

namespace RevitSyncService.Core.Models
{
    public class SourceConfig
    {
        /// <summary>
        /// "RevitServer" или "NetworkFolder"
        /// </summary>
        public string Type { get; set; } = "NetworkFolder";

        /// <summary>
        /// Для RevitServer: адрес сервера (например "192.168.1.100")
        /// </summary>
        public string? Server { get; set; }

        /// <summary>
        /// Для RevitServer: версия Revit ("2023", "2024", "2025", "2026")
        /// </summary>
        public string? RevitVersion { get; set; }

        /// <summary>
        /// Путь к папке.
        /// RevitServer: "/ЖК_Северный/АР"
        /// NetworkFolder: "\\server\projects\Северный"
        /// </summary>
        public string FolderPath { get; set; } = string.Empty;

        /// <summary>
        /// Исключаемые подпапки
        /// </summary>
        public List<string> ExcludedFolders { get; set; } = new() { "Прочее" };

        public bool IsRevitServer => Type == "RevitServer";
    }
}
