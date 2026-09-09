using System;
using System.IO;
using RevitSyncService.Core.Models;

namespace RevitSyncService.Core.Services
{
    /// <summary>
    /// Хранит настройки подключения к БД локально в AppData пользователя.
    /// Аналог старого config_path.txt, но для строки подключения.
    /// </summary>
    public class DatabaseConnectionService
    {
        private static readonly string ConnectionFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RevitSyncService",
            "db_connection.json"
        );

        private DbConnectionConfig? _config;

        public DbConnectionConfig? Config => _config;

        /// <summary>
        /// Загрузить сохранённые настройки.
        /// Возвращает false если файл не найден (первый запуск).
        /// </summary>
        public bool Load()
        {
            if (!File.Exists(ConnectionFilePath))
                return false;

            try
            {
                string json = File.ReadAllText(ConnectionFilePath);
                _config = DbConnectionConfig.Deserialize(json);
                return _config != null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Сохранить настройки подключения
        /// </summary>
        public void Save(DbConnectionConfig config)
        {
            _config = config;
            string? dir = Path.GetDirectoryName(ConnectionFilePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(ConnectionFilePath, config.Serialize());
        }

        /// <summary>
        /// Проверить соединение с БД
        /// </summary>
        public static async System.Threading.Tasks.Task<(bool Success, string Message)> TestConnectionAsync(DbConnectionConfig config)
        {
            try
            {
                await using var conn = new Npgsql.NpgsqlConnection(config.ToConnectionString());
                await conn.OpenAsync();
                return (true, "Соединение успешно!");
            }
            catch (Exception ex)
            {
                return (false, $"Ошибка: {ex.Message}");
            }
        }
    }
}