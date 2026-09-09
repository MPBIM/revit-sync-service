using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RevitSyncService.Infrastructure.Navisworks
{
    public class FileToolsWrapper
    {
        static FileToolsWrapper()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        public async Task<bool> ConvertToNwcAsync(string rvtPath, string nwcOutputPath,
            string preferredVersion, CancellationToken ct = default)
        {
            string toolPath = FindFileToolsRunner(preferredVersion, out string foundVersion);
            if (string.IsNullOrEmpty(toolPath))
                throw new FileNotFoundException("FileToolsTaskRunner.exe не найден.");

            if (!File.Exists(rvtPath))
                throw new FileNotFoundException($"RVT файл не найден: {rvtPath}");

            // Создаём папку назначения для NWC
            string nwcDir = Path.GetDirectoryName(nwcOutputPath)!;
            Directory.CreateDirectory(nwcDir);

            string winTemp = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp");

            // TXT файл в UTF-8 (как CHCP 65001 в батнике)
            string tempTxt = Path.Combine(winTemp, $"nwc_{Guid.NewGuid():N}.txt");
            await File.WriteAllTextAsync(tempTxt, rvtPath, new UTF8Encoding(false), ct);

            // NWD во временную папку (ASCII путь) — побочный продукт, нам не нужен
            string tempNwd = Path.Combine(winTemp, $"nwc_{Guid.NewGuid():N}.nwd");

            // /i = TXT список, /of = NWD (NWC создаётся рядом с RVT автоматически)
            string arguments = $"/i \"{tempTxt}\" /of \"{tempNwd}\" /over";

            Debug.WriteLine($"[NWC] Tool: {toolPath} (v{foundVersion})");
            Debug.WriteLine($"[NWC] RVT: {rvtPath}");
            Debug.WriteLine($"[NWC] Output: {nwcOutputPath}");
            Debug.WriteLine($"[NWC] Args: {arguments}");

            string stdout = string.Empty;
            string stderr = string.Empty;
            int exitCode = -1;

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = toolPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.GetEncoding(866),
                    StandardErrorEncoding = Encoding.GetEncoding(866),
                    WorkingDirectory = Path.GetDirectoryName(toolPath)!
                };

                using var process = new Process { StartInfo = psi };
                process.Start();

                stdout = await process.StandardOutput.ReadToEndAsync();
                stderr = await process.StandardError.ReadToEndAsync();
                await WaitForExitAsync(process, ct);
                exitCode = process.ExitCode;

                Debug.WriteLine($"[NWC] ExitCode: {exitCode}");
                Debug.WriteLine($"[NWC] StdOut: {stdout}");
                if (!string.IsNullOrWhiteSpace(stderr))
                    Debug.WriteLine($"[NWC] StdErr: {stderr}");
            }
            finally
            {
                // Чистим временные файлы
                try { if (File.Exists(tempTxt)) File.Delete(tempTxt); } catch { }
                try { if (File.Exists(tempNwd)) File.Delete(tempNwd); } catch { }
            }

            // NWC создаётся РЯДОМ С RVT файлом — это поведение FileToolsTaskRunner
            string rvtDir = Path.GetDirectoryName(rvtPath)!;
            string rvtNameWithoutExt = Path.GetFileNameWithoutExtension(rvtPath);
            string createdNwcPath = Path.Combine(rvtDir, rvtNameWithoutExt + ".nwc");

            if (!File.Exists(createdNwcPath))
            {
                throw new FileNotFoundException(
                    $"NWC не найден рядом с RVT: {createdNwcPath}\n" +
                    $"ExitCode: {exitCode}\n" +
                    $"StdOut: {stdout}\n" +
                    $"StdErr: {stderr}");
            }

            // Перемещаем NWC из папки RVT в папку NWC
            if (File.Exists(nwcOutputPath))
                File.Delete(nwcOutputPath);

            await Task.Run(() => File.Move(createdNwcPath, nwcOutputPath), ct);
            Debug.WriteLine($"[NWC] Перемещён: {createdNwcPath} → {nwcOutputPath}");

            return true;
        }

        public static string FindFileToolsRunner(string preferredVersion, out string foundVersion)
        {
            string path = GetFileToolsPath(preferredVersion);
            if (File.Exists(path)) { foundVersion = preferredVersion; return path; }

            string[] versions = { "2026", "2025", "2024", "2023", "2022", "2021", "2020" };
            foreach (var ver in versions)
            {
                path = GetFileToolsPath(ver);
                if (File.Exists(path)) { foundVersion = ver; return path; }
            }

            foundVersion = string.Empty;
            return string.Empty;
        }

        public static bool IsNavisworksAvailable(string preferredVersion = "2025")
            => !string.IsNullOrEmpty(FindFileToolsRunner(preferredVersion, out _));

        private static string GetFileToolsPath(string version)
            => Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Autodesk",
                $"Navisworks Manage {version}",
                "FileToolsTaskRunner.exe");

        private static async Task WaitForExitAsync(Process process, CancellationToken ct)
        {
            try { await process.WaitForExitAsync(ct); }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                throw;
            }
        }
    }
}