using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace RevitSyncService.Infrastructure.RevitServer
{
    /// <summary>
    /// Обёртка над RevitServerTool.exe для скачивания файлов с Revit Server.
    /// HTTP API используется ТОЛЬКО для просмотра — скачивание ТОЛЬКО через этот инструмент.
    /// </summary>
    public class RevitServerToolWrapper
    {
        /// <summary>
        /// Скачать файл с Revit Server
        /// </summary>
        /// <param name="serverName">Имя/адрес сервера</param>
        /// <param name="rsnPath">Путь в формате RSN://server/path/file.rvt</param>
        /// <param name="destinationPath">Локальный путь для сохранения</param>
        /// <param name="revitVersion">Версия Revit (для поиска RevitServerTool.exe)</param>
        /// <param name="ct">Токен отмены</param>
        public async Task<bool> DownloadFileAsync(string serverName, string modelPath, string destinationPath, string revitVersion, CancellationToken ct = default)
        {
            string toolPath = GetRevitServerToolPath(revitVersion);
            if (!File.Exists(toolPath))
                throw new FileNotFoundException($"RevitServerTool.exe не найден: {toolPath}");

            string? dir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            // Правильный формат: createLocalRVT "modelPath" -s "server" -d "dest" -o
            // modelPath — путь относительно корня сервера, например "Project\SubFolder\Model.rvt"
            string arguments = $"createLocalRVT \"{modelPath}\" -s \"{serverName}\" -d \"{destinationPath}\" -o";

            System.Diagnostics.Debug.WriteLine($"[RST] Tool: {toolPath}");
            System.Diagnostics.Debug.WriteLine($"[RST] Args: {arguments}");

            var psi = new ProcessStartInfo
            {
                FileName = toolPath,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8
            };

            using var process = new Process { StartInfo = psi };
            process.Start();

            string stdout = await process.StandardOutput.ReadToEndAsync();
            string stderr = await process.StandardError.ReadToEndAsync();

            await WaitForExitAsync(process, ct);

            System.Diagnostics.Debug.WriteLine($"[RST] ExitCode: {process.ExitCode}");
            System.Diagnostics.Debug.WriteLine($"[RST] StdOut: {stdout}");
            System.Diagnostics.Debug.WriteLine($"[RST] StdErr: {stderr}");
            System.Diagnostics.Debug.WriteLine($"[RST] File exists: {File.Exists(destinationPath)}");

            if (process.ExitCode != 0)
            {
                string errorMsg = !string.IsNullOrEmpty(stderr) ? stderr : stdout;
                throw new InvalidOperationException($"RevitServerTool код {process.ExitCode}: {errorMsg}");
            }

            bool fileExists = File.Exists(destinationPath);
            if (!fileExists)
            {
                throw new FileNotFoundException(
                    $"RevitServerTool завершился (код 0), но файл не создан.\n" +
                    $"Команда: {arguments}\nStdOut: {stdout}");
            }

            return true;
        }
        /// <summary>
        /// Получить путь к RevitServerTool.exe для конкретной версии
        /// </summary>
        public static string GetRevitServerToolPath(string revitVersion)
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Autodesk",
                $"Revit {revitVersion}",
                "RevitServerToolCommand",
                "RevitServerTool.exe"
            );
        }

        /// <summary>
        /// Проверить, доступен ли RevitServerTool для данной версии
        /// </summary>
        public static bool IsAvailable(string revitVersion)
        {
            return File.Exists(GetRevitServerToolPath(revitVersion));
        }

        /// <summary>
        /// Async-ожидание завершения процесса с поддержкой CancellationToken
        /// </summary>
        private static async Task WaitForExitAsync(Process process, CancellationToken ct)
        {
            try
            {
                await process.WaitForExitAsync(ct);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                throw;
            }
        }
    }
}
