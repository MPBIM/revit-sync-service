namespace RevitSyncService.Core.Models
{
    public class DestinationConfig
    {
        /// <summary>
        /// Куда сохранять RVT файлы
        /// </summary>
        public string RvtPath { get; set; } = string.Empty;

        /// <summary>
        /// Куда сохранять NWC файлы
        /// </summary>
        public string NwcPath { get; set; } = string.Empty;

        /// <summary>
        /// Нужно ли конвертировать в NWC
        /// </summary>
        public bool CreateNwc { get; set; } = true;
    }
}
