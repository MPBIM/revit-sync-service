using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RevitSyncService.Core.Interfaces;
using RevitSyncService.Core.Models;
using RevitSyncService.Infrastructure.RevitServer;

namespace RevitSyncService.Infrastructure.FileSystem
{
    public class DownloadService : IDownloadService
    {
        private readonly RevitServerToolWrapper _toolWrapper = new();
        private readonly ILogService _log;

        public DownloadService(ILogService log)
        {
            _log = log;
        }

        public async Task<List<string>> DownloadFilesAsync(Project project, IProgress<ProgressInfo>? progress, CancellationToken ct)
        {
            if (project.Source.IsRevitServer)
                return await DownloadFromRevitServerAsync(project, progress, ct);
            else
                return await CopyFromNetworkFolderAsync(project, progress, ct);
        }

        /// <summary>
        /// Скачивание с Revit Server: HTTP API (список файлов) + RevitServerTool (скачивание)
        /// </summary>
        private async Task<List<string>> DownloadFromRevitServerAsync(Project project, IProgress<ProgressInfo>? progress, CancellationToken ct)
        {
            var downloadedFiles = new List<string>();
            var source = project.Source;

            using var apiService = new RevitServerApiService(source.Server!, source.RevitVersion!);
            var contents = await apiService.GetFolderContentsAsync(source.FolderPath);

            // Теперь собираем (ModelPath, FileName) — ModelPath для RevitServerTool
            var allModels = new List<(string ModelPath, string FileName)>();
            await CollectModelsRecursive(apiService, source.FolderPath, contents, allModels, source.ExcludedFolders, ct);

            if (allModels.Count == 0)
            {
                _log.Warning("На сервере не найдено RVT файлов", project.Name);
                return downloadedFiles;
            }

            var progressInfo = new ProgressInfo
            {
                TotalFiles = allModels.Count,
                IsRunning = true
            };

            foreach (var (modelPath, fileName) in allModels)
            {
                ct.ThrowIfCancellationRequested();

                string destPath = Path.Combine(project.Destination.RvtPath, fileName);
                progressInfo.CurrentFile = fileName;
                progressInfo.CurrentOperation = "Скачивание с Revit Server...";
                progress?.Report(progressInfo);

                try
                {
                    _log.Info($"Модель: \"{modelPath}\" → {destPath}", project.Name);

                    // Передаём modelPath (например "24001_Х1\КЖ\Model.rvt"), не RSN://
                    await _toolWrapper.DownloadFileAsync(
                        source.Server!, modelPath, destPath, source.RevitVersion!, ct);

                    downloadedFiles.Add(destPath);
                    progressInfo.SuccessCount++;
                    _log.Success($"Скачан: {fileName}", project.Name);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    progressInfo.ErrorCount++;
                    _log.Error($"Ошибка скачивания {fileName}: {ex.Message}", project.Name, ex.ToString());
                }

                progressInfo.ProcessedFiles++;
                progress?.Report(progressInfo);
            }

            return downloadedFiles;
        }
        /// <summary>
        /// Рекурсивный сбор моделей с сервера
        /// </summary>
        private async Task CollectModelsRecursive(
            RevitServerApiService api,
            string currentFolderPath,
            FolderContents contents,
            List<(string ModelPath, string FileName)> models,
            List<string> excludedFolders,
            CancellationToken ct)
        {
            // Добавить модели из текущей папки
            foreach (var model in contents.Models)
            {
                // Строим путь сами: "FolderA\SubFolder\Model.rvt"
                // currentFolderPath приходит как "/FolderA/SubFolder"
                string relativePath = currentFolderPath.TrimStart('/').Replace("/", "\\");
                string modelPath = string.IsNullOrEmpty(relativePath)
                    ? model.Name
                    : $"{relativePath}\\{model.Name}";

                models.Add((modelPath, model.Name));
            }

            // Рекурсивно обойти подпапки
            foreach (var folder in contents.Folders)
            {
                ct.ThrowIfCancellationRequested();

                if (excludedFolders.Any(ex =>
                    folder.Name.Equals(ex, StringComparison.OrdinalIgnoreCase)))
                    continue;

                try
                {
                    string subPath = currentFolderPath.TrimEnd('/') + "/" + folder.Name;
                    var subContents = await api.GetFolderContentsAsync(subPath);
                    await CollectModelsRecursive(api, subPath, subContents, models, excludedFolders, ct);
                }
                catch (Exception)
                {
                    // Пропускаем недоступные
                }
            }
        }
        /// <summary>
        /// Копирование из сетевой папки
        /// </summary>
        private async Task<List<string>> CopyFromNetworkFolderAsync(Project project, IProgress<ProgressInfo>? progress, CancellationToken ct)
        {
            var downloadedFiles = new List<string>();
            var source = project.Source;

            if (!Directory.Exists(source.FolderPath))
            {
                _log.Error($"Папка не найдена: {source.FolderPath}", project.Name);
                return downloadedFiles;
            }

            // Получаем все .rvt файлы, исключая подпапки из excludedFolders
            var rvtFiles = Directory.GetFiles(source.FolderPath, "*.rvt", SearchOption.AllDirectories)
                .Where(f => !IsInExcludedFolder(f, source.FolderPath, source.ExcludedFolders))
                .ToList();

            if (rvtFiles.Count == 0)
            {
                _log.Warning("В папке не найдено RVT файлов", project.Name);
                return downloadedFiles;
            }

            var progressInfo = new ProgressInfo
            {
                TotalFiles = rvtFiles.Count,
                IsRunning = true
            };

            Directory.CreateDirectory(project.Destination.RvtPath);

            foreach (var filePath in rvtFiles)
            {
                ct.ThrowIfCancellationRequested();

                string fileName = Path.GetFileName(filePath);
                string destPath = Path.Combine(project.Destination.RvtPath, fileName);

                progressInfo.CurrentFile = fileName;
                progressInfo.CurrentOperation = "Копирование файла...";
                progress?.Report(progressInfo);

                try
                {
                    await Task.Run(() => File.Copy(filePath, destPath, overwrite: true), ct);

                    downloadedFiles.Add(destPath);
                    progressInfo.SuccessCount++;
                    _log.Success($"Скопирован: {fileName}", project.Name);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    progressInfo.ErrorCount++;
                    _log.Error($"Ошибка копирования {fileName}: {ex.Message}", project.Name, ex.ToString());
                }

                progressInfo.ProcessedFiles++;
                progress?.Report(progressInfo);
            }

            return downloadedFiles;
        }

        private static bool IsInExcludedFolder(string filePath, string basePath, List<string> excluded)
        {
            string relativePath = Path.GetRelativePath(basePath, filePath);
            return excluded.Any(ex =>
                relativePath.Split(Path.DirectorySeparatorChar)
                    .Any(part => part.Equals(ex, StringComparison.OrdinalIgnoreCase)));
        }
    }
}
