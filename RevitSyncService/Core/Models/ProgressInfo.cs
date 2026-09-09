namespace RevitSyncService.Core.Models
{
    public class ProgressInfo
    {
        public string CurrentFile { get; set; } = string.Empty;
        public string CurrentOperation { get; set; } = string.Empty;
        public int TotalFiles { get; set; }
        public int ProcessedFiles { get; set; }
        public int SuccessCount { get; set; }
        public int ErrorCount { get; set; }
        public double Percentage => TotalFiles > 0 ? (double)ProcessedFiles / TotalFiles * 100 : 0;
        public bool IsRunning { get; set; }
    }
}
