using System;

namespace RevitSyncService.Core.Models
{
    public class Project
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public bool Enabled { get; set; } = true;

        public SourceConfig Source { get; set; } = new();
        public DestinationConfig Destination { get; set; } = new();
        public ScheduleConfig Schedule { get; set; } = new();

        public DateTime? LastRun { get; set; }
        public DateTime? NextRun { get; set; }
        public ProjectStatus Status { get; set; } = ProjectStatus.Waiting;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Краткое описание источника для отображения в DataGrid
        /// </summary>
        public string SourceDisplay
        {
            get
            {
                if (Source.IsRevitServer)
                    return $"RSN://{Source.Server}{Source.FolderPath}";
                return Source.FolderPath;
            }
        }

        /// <summary>
        /// Краткое описание расписания для отображения
        /// </summary>
        public string ScheduleDisplay
        {
            get
            {
                if (Schedule.Type == "Paused")
                    return "⏸ Приостановлен";

                var days = new[] { "Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Вс" };
                string day = (Schedule.DayOfWeek >= 0 && Schedule.DayOfWeek < 7) ? days[Schedule.DayOfWeek] : "?";
                string prefix = Schedule.Type == "Biweekly" ? "Раз в 2 нед." : "Еженед.";
                return $"{prefix} {day} {Schedule.Time}";
            }
        }

        /// <summary>
        /// Позиция в очереди (0 = не в очереди)
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public int QueuePosition { get; set; } = 0;

        /// <summary>
        /// Отображение статуса с учётом очереди
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public string StatusDisplay
        {
            get
            {
                return Status switch
                {
                    ProjectStatus.Running => "▶ Выполняется",
                    ProjectStatus.Queued => $"Очередь {QueuePosition}",
                    ProjectStatus.Completed => "Завершён",
                    ProjectStatus.Failed => "Ошибка",
                    ProjectStatus.Cancelled => "Отменён",
                    ProjectStatus.Waiting => "Ожидание",
                    _ => "—"
                };
            }
        }
    }
}
