using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using RevitSyncService.Core.Models;
using RevitSyncService.Infrastructure.Database;

namespace RevitSyncService.Core.Services
{
    public class ConfigService
    {
        private DbRepository? _repository;
        private AppConfig _config = new();

        public AppConfig Config => _config;
        public string ConfigFilePath => "PostgreSQL: tables global_settings + projects";

        private static readonly string LocalSettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RevitSyncService",
            "local_settings.json"
        );

        public bool Initialize(string? connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
                return false;

            try
            {
                _repository = new DbRepository(connectionString);
                Task.Run(() => _repository.EnsureSchemaAsync()).GetAwaiter().GetResult();
                Load();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConfigService] Init failed: {ex.Message}");
                _repository = null;
                return false;
            }
        }

        public void Load()
        {
            if (_repository == null) return;
            try
            {
                _config.GlobalSettings = _repository.LoadGlobalSettings();
                _config.Projects = _repository.LoadProjects();
                System.Diagnostics.Debug.WriteLine($"[ConfigService] Загружено проектов: {_config.Projects.Count}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConfigService] Ошибка Load: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[ConfigService] Stack: {ex.StackTrace}");
                _config = new AppConfig();
            }
        }

        public void Save()
        {
            if (_repository == null) return;
            _repository.SaveGlobalSettings(_config.GlobalSettings);
        }

        public void SaveLocalSettings()
        {
            try
            {
                var dir = Path.GetDirectoryName(LocalSettingsPath)!;
                Directory.CreateDirectory(dir);
                var json = System.Text.Json.JsonSerializer.Serialize(new
                {
                    AutoExportEnabled = _config.GlobalSettings.AutoExportEnabled
                });
                File.WriteAllText(LocalSettingsPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConfigService] Ошибка SaveLocalSettings: {ex.Message}");
            }
        }

        public void LoadLocalSettings()
        {
            try
            {
                if (!File.Exists(LocalSettingsPath)) return;
                var json = File.ReadAllText(LocalSettingsPath);
                var doc = System.Text.Json.JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("AutoExportEnabled", out var val))
                    _config.GlobalSettings.AutoExportEnabled = val.GetBoolean();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConfigService] Ошибка LoadLocalSettings: {ex.Message}");
            }
        }

        public DbRepository? Repository => _repository;

        public string GetExpandedTempFolder()
            => Environment.ExpandEnvironmentVariables(_config.GlobalSettings.TempFolder);

        public string GetExpandedLogFolder()
            => Environment.ExpandEnvironmentVariables(_config.GlobalSettings.LogFolder);
    }
}