using System;

namespace RevitSyncService.Core.Models
{
    /// <summary>
    /// Результат попытки захвата объекта или просто текущее состояние блокировки.
    /// Один и тот же механизм используется и для ВПК (авто-очередь), и для БПК (ручной запуск).
    /// </summary>
    public class ProjectLockInfo
    {
        public string ProjectId { get; set; } = string.Empty;

        /// <summary>
        /// true — блокировку захватили именно мы этим вызовом.
        /// false — объект занят кем-то другим (см. LockedBy/LockedAt).
        /// </summary>
        public bool Acquired { get; set; }

        /// <summary>
        /// Кто держит блокировку сейчас (например "ВПК-2 / svc_bim" или "DESKTOP-БПК07 / Ivanov").
        /// null — объект свободен.
        /// </summary>
        public string? LockedBy { get; set; }

        public DateTime? LockedAt { get; set; }

        public bool IsLocked => !string.IsNullOrEmpty(LockedBy);
    }
}