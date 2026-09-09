using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using RevitSyncService.Core.Interfaces;
using RevitSyncService.Core.Models;
using RevitSyncService.Infrastructure.Navisworks;

namespace RevitSyncService.Infrastructure.FileSystem
{
    public class ConversionService : IConversionService
    {
        private readonly FileToolsWrapper _wrapper = new();
        private readonly ILogService _log;

        public ConversionService(ILogService log)
        {
            _log = log;
        }

        public async Task<int> ConvertToNwcAsync(List<string> rvtFiles, string nwcOutputFolder,
            string revitVersion, IProgress<ProgressInfo>? progress, CancellationToken ct)
        {
            if (!FileToolsWrapper.IsNavisworksAvailable(revitVersion))
            {
                _log.Warning("Navisworks не найден — конвертация пропущена");
                return 0;
            }

            Directory.CreateDirectory(nwcOutputFolder);
            int successCount = 0;

            for (int i = 0; i < rvtFiles.Count; i++)
            {
                ct.ThrowIfCancellationRequested();

                string rvtPath = rvtFiles[i];
                string fileName = Path.GetFileNameWithoutExtension(rvtPath) + ".nwc";
                string nwcPath = Path.Combine(nwcOutputFolder, fileName);

                progress?.Report(new ProgressInfo
                {
                    CurrentFile = Path.GetFileName(rvtPath),
                    CurrentOperation = $"Конвертация в NWC ({i + 1}/{rvtFiles.Count})...",
                    TotalFiles = rvtFiles.Count,
                    ProcessedFiles = i,
                    SuccessCount = successCount,
                    IsRunning = true
                });

                try
                {
                    bool ok = await _wrapper.ConvertToNwcAsync(rvtPath, nwcPath, revitVersion, ct);
                    if (ok)
                    {
                        successCount++;
                        _log.Success($"Конвертирован: {fileName}");
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    _log.Error($"Ошибка конвертации {Path.GetFileName(rvtPath)}: {ex.Message}",
                        details: ex.ToString());
                }
            }

            return successCount;
        }

        public bool IsNavisworksAvailable(string revitVersion = "2025")
            => FileToolsWrapper.IsNavisworksAvailable(revitVersion);
    }
}