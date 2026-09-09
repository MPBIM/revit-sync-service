namespace RevitSyncService.Core.Models
{
    public class DbConnectionConfig
    {
        public string Host { get; set; } = "localhost";
        public int Port { get; set; } = 5432;
        public string Database { get; set; } = "progress";
        public string Username { get; set; } = "progress";
        public string Password { get; set; } = "12345678";

        public string ToConnectionString() =>
    $"Host={Host};" +
    $"Port={Port};" +
    $"Database={Database};" +
    $"Username={Username};" +
    $"Password={Password};" +
    $"Timeout=5;" +
    $"Command Timeout=10;" +
    $"Pooling=true;" +
    $"Minimum Pool Size=1;" +
    $"Maximum Pool Size=10;";  // ← ограничиваем пул

        /// <summary>
        /// Сохранить в одну строку для хранения в файле
        /// </summary>
        public string Serialize()
            => System.Text.Json.JsonSerializer.Serialize(this);

        public static DbConnectionConfig? Deserialize(string json)
        {
            try { return System.Text.Json.JsonSerializer.Deserialize<DbConnectionConfig>(json); }
            catch { return null; }
        }
    }
}