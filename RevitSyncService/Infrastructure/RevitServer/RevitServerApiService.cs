using Newtonsoft.Json;
using RevitSyncService.Core.Models;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace RevitSyncService.Infrastructure.RevitServer
{
    /// <summary>
    /// HTTP REST API клиент для просмотра папок на Revit Server.
    /// ТОЛЬКО для просмотра! Скачивание — через RevitServerToolWrapper.
    /// </summary>
    public class RevitServerApiService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _serverName;
        private readonly string _revitVersion;

        public RevitServerApiService(string serverName, string revitVersion)
        {
            _serverName = serverName;
            _revitVersion = revitVersion;

            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true,
                UseDefaultCredentials = true
            };

            _httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(60)
            };

            // Обязательные заголовки для Revit Server REST API
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", $"Autodesk Revit/{revitVersion}");
            _httpClient.DefaultRequestHeaders.Add("User-Name", SanitizeHeaderValue(Environment.UserName));
            _httpClient.DefaultRequestHeaders.Add("User-Machine-Name", SanitizeHeaderValue(Environment.MachineName));
            _httpClient.DefaultRequestHeaders.Add("Operation-GUID", Guid.NewGuid().ToString());
        }

        private string BaseUrl =>
            $"http://{_serverName}/RevitServerAdminRESTService{_revitVersion}/AdminRESTService.svc";

        /// <summary>
        /// Получить содержимое корневой папки
        /// </summary>
        public async Task<FolderContents> GetRootContentsAsync()
        {
            return await GetFolderContentsAsync("");
        }

        /// <summary>
        /// Получить содержимое указанной папки
        /// </summary>
        public async Task<FolderContents> GetFolderContentsAsync(string folderPath)
        {
            string encodedPath = EncodeFolderPath(folderPath);

            // Корневая папка = "|", подпапка = "encodedPath"
            string pathPart = string.IsNullOrEmpty(encodedPath) ? "|" : encodedPath;
            string url = $"{BaseUrl}/{pathPart}/contents";

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync();
            var contents = JsonConvert.DeserializeObject<FolderContents>(json);
            return contents ?? new FolderContents();
        }

        /// <summary>
        /// Проверить доступность сервера
        /// </summary>
        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                string url = $"{BaseUrl}/serverProperties";
                var response = await _httpClient.GetAsync(url);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Конвертировать API-путь в RSN-путь для RevitServerTool.
        /// API возвращает пути с pipe "|", нужно преобразовать в формат RSN://server/path/file.rvt
        /// </summary>
        public string ConvertToRsnPath(string apiPath)
        {
            string path = apiPath.TrimStart('|').Replace("|", "/");
            if (!path.StartsWith("/"))
                path = "/" + path;
            return $"RSN://{_serverName}{path}";
        }

        /// <summary>
        /// Кодирование пути для URL: "/" заменяется на "|"
        /// </summary>
        private string EncodeFolderPath(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath))
                return string.Empty;

            // Заменяем "/" на "|", затем каждый сегмент кодируем отдельно
            string path = folderPath.TrimStart('/');
            var segments = path.Split('/');
            var encoded = segments.Select(s => Uri.EscapeDataString(s));
            return string.Join("|", encoded);
        }

        /// <summary>
        /// Очистка значения для HTTP заголовка
        /// </summary>
        private static string SanitizeHeaderValue(string value)
        {
            if (string.IsNullOrEmpty(value)) return "Unknown";
            // Удаляем непечатаемые символы
            return System.Text.RegularExpressions.Regex.Replace(value, @"[^\x20-\x7E]", "_");
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
