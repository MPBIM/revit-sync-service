using System.Collections.Generic;
using Newtonsoft.Json;

namespace RevitSyncService.Core.Models
{
    /// <summary>
    /// Ответ Revit Server API на запрос содержимого папки
    /// </summary>
    public class FolderContents
    {
        [JsonProperty("Path")]
        public string Path { get; set; } = string.Empty;

        [JsonProperty("DriveFreeSpace")]
        public long DriveFreeSpace { get; set; }

        [JsonProperty("DriveSpace")]
        public long DriveSpace { get; set; }

        [JsonProperty("Folders")]
        public List<FolderInfo> Folders { get; set; } = new();

        [JsonProperty("Models")]
        public List<ModelInfo> Models { get; set; } = new();

        [JsonProperty("Files")]
        public List<object> Files { get; set; } = new();
    }

    public class FolderInfo
    {
        [JsonProperty("Name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("Path")]
        public string Path { get; set; } = string.Empty;

        [JsonProperty("Size")]
        public long Size { get; set; }

        [JsonProperty("HasContents")]
        public bool HasContents { get; set; }
    }

    public class ModelInfo
    {
        [JsonProperty("Name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("Path")]
        public string Path { get; set; } = string.Empty;

        [JsonProperty("Size")]
        public long Size { get; set; }

        [JsonProperty("ModelSize")]
        public long ModelSize { get; set; }

        [JsonProperty("ProductVersion")]
        public int ProductVersion { get; set; }
    }

    /// <summary>
    /// Информация о сервере из RSN.ini
    /// </summary>
    public class RevitServerInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Host { get; set; } = string.Empty;
        public string RevitVersion { get; set; } = string.Empty;

        public string DisplayName => $"{Name} ({Host}) — Revit {RevitVersion}";
    }
}
