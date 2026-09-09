using System;

namespace RevitSyncService.Core.Models
{
    public class ScheduleConfig
    {
        /// <summary>
        /// "Weekly" или "Biweekly"
        /// </summary>
        public string Type { get; set; } = "Weekly";

        /// <summary>
        /// День недели: 0 = Понедельник, 6 = Воскресенье
        /// </summary>
        public int DayOfWeek { get; set; } = 0;

        /// <summary>
        /// Время запуска в формате "HH:mm"
        /// </summary>
        public string Time { get; set; } = "10:00";

        /// <summary>
        /// Вычислить следующее время запуска
        /// </summary>
        public DateTime GetNextRunTime(DateTime? lastRun = null)
        {
            var timeParts = Time.Split(':');
            int hour = int.Parse(timeParts[0]);
            int minute = int.Parse(timeParts[1]);

            // Целевой DayOfWeek (System.DayOfWeek: Sunday=0, Monday=1...)
            // Наш DayOfWeek: 0=Monday, 6=Sunday
            var targetDow = DayOfWeek == 6 ? System.DayOfWeek.Sunday : (System.DayOfWeek)(DayOfWeek + 1);

            DateTime baseDate = lastRun ?? DateTime.Now;
            DateTime candidate = baseDate.Date.AddHours(hour).AddMinutes(minute);

            // Найти ближайший нужный день недели
            int daysUntilTarget = ((int)targetDow - (int)candidate.DayOfWeek + 7) % 7;

            if (daysUntilTarget == 0 && candidate <= DateTime.Now)
            {
                // Этот день уже прошёл — следующая неделя
                daysUntilTarget = Type == "Biweekly" ? 14 : 7;
            }
            else if (daysUntilTarget == 0 && lastRun.HasValue)
            {
                daysUntilTarget = Type == "Biweekly" ? 14 : 7;
            }

            if (daysUntilTarget > 0 && Type == "Biweekly" && lastRun.HasValue)
            {
                // Если biweekly и прошло меньше 14 дней — скипнуть
                var daysSinceLast = (DateTime.Now - lastRun.Value).TotalDays;
                if (daysSinceLast < 13)
                {
                    daysUntilTarget += 7;
                }
            }

            return candidate.AddDays(daysUntilTarget);
        }

        /// <summary>
        /// Проверить, пора ли запускать проект
        /// </summary>
        public bool IsDueNow(DateTime? lastRun, DateTime? nextRun)
        {
            if (Type == "Paused") return false;
            if (nextRun.HasValue)
            {
                return DateTime.Now >= nextRun.Value;
            }

            // Если nextRun не задан — вычислить
            var next = GetNextRunTime(lastRun);
            return DateTime.Now >= next;
        }
    }
}
