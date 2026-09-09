using System;
using System.Collections.Generic;
using System.IO;
using RevitSyncService.Core.Models;

namespace RevitSyncService.Infrastructure.RevitServer
{
    /// <summary>
    /// Парсер RSN.ini файлов для обнаружения серверов Revit Server.
    /// RSN.ini располагается в C:\ProgramData\Autodesk\Revit Server YYYY\Config\RSN.ini
    /// </summary>
    public static class RsnIniParser
    {
        private static readonly string[] SupportedVersions = { "2023", "2024", "2025", "2026" };

        /// <summary>
        /// Найти все RSN.ini файлы и извлечь список серверов
        /// </summary>
        public static List<RevitServerInfo> DiscoverServers()
        {
            var servers = new List<RevitServerInfo>();

            foreach (var version in SupportedVersions)
            {
                string iniPath = GetRsnIniPath(version);
                if (!File.Exists(iniPath)) continue;

                try
                {
                    var lines = File.ReadAllLines(iniPath);
                    foreach (var rawLine in lines)
                    {
                        string line = rawLine.Trim();
                        if (string.IsNullOrEmpty(line) || line.StartsWith(";") || line.StartsWith("#"))
                            continue;

                        // RSN.ini может содержать просто IP/hostname, по одному на строку
                        servers.Add(new RevitServerInfo
                        {
                            Name = line,
                            Host = line,
                            RevitVersion = version
                        });
                    }
                }
                catch (Exception)
                {
                    // Пропускаем неспарсенные файлы
                }
            }

            return servers;
        }

        /// <summary>
        /// Путь к RSN.ini для конкретной версии Revit
        /// </summary>
        public static string GetRsnIniPath(string revitVersion)
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Autodesk",
                $"Revit Server {revitVersion}",
                "Config",
                "RSN.ini"
            );
        }

        /// <summary>
        /// Получить список доступных версий Revit (у которых есть RSN.ini)
        /// </summary>
        public static List<string> GetAvailableVersions()
        {
            var versions = new List<string>();
            foreach (var version in SupportedVersions)
            {
                if (File.Exists(GetRsnIniPath(version)))
                    versions.Add(version);
            }
            return versions;
        }
    }
}
