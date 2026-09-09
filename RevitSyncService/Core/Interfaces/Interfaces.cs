using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RevitSyncService.Core.Models;

namespace RevitSyncService.Core.Interfaces
{
    public interface IRevitServerService
    {
        Task<FolderContents> GetFolderContentsAsync(string serverName, string revitVersion, string folderPath);
        Task<FolderContents> GetRootContentsAsync(string serverName, string revitVersion);
        Task<bool> TestConnectionAsync(string serverName, string revitVersion);
    }

    public interface IDownloadService
    {
        /// <summary>
        /// Скачать файлы для проекта (RevitServer или NetworkFolder)
        /// </summary>
        Task<List<string>> DownloadFilesAsync(Project project, IProgress<ProgressInfo>? progress, CancellationToken ct);
    }

    public interface IConversionService
    {
        Task<int> ConvertToNwcAsync(List<string> rvtFiles, string nwcOutputFolder,
            string revitVersion, IProgress<ProgressInfo>? progress, CancellationToken ct);

        bool IsNavisworksAvailable(string revitVersion = "2025");
    }
    public interface ILogService
    {
        void Log(LogLevel level, string message, string? projectName = null, string? details = null);
        void Info(string message, string? projectName = null);
        void Success(string message, string? projectName = null);
        void Warning(string message, string? projectName = null);
        void Error(string message, string? projectName = null, string? details = null);
        IReadOnlyList<LogEntry> GetEntries();
        void Clear();
        event Action<LogEntry>? OnNewEntry;
    }
}
