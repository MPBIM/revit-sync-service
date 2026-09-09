using System;
using System.Threading;
using System.Threading.Tasks;
using RevitSyncService.Core.Interfaces;

namespace RevitSyncService.Core.Services
{
    /// <summary>
    /// Планировщик: проверяет каждую минуту, какие проекты готовы к выполнению
    /// </summary>
    public class Scheduler : IDisposable
    {
        private readonly ProjectManager _projectManager;
        private readonly QueueManager _queueManager;
        private readonly ILogService _log;
        private Timer? _timer;
        private bool _isChecking;

        public bool IsEnabled { get; set; } = true;

        public Scheduler(ProjectManager projectManager, QueueManager queueManager, ILogService log)
        {
            _projectManager = projectManager;
            _queueManager = queueManager;
            _log = log;
        }

        /// <summary>
        /// Запустить планировщик
        /// </summary>
        public void Start()
        {
            _timer = new Timer(OnTimerTick, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
            _log.Info("Планировщик запущен");
        }

        /// <summary>
        /// Остановить планировщик
        /// </summary>
        public void Stop()
        {
            _timer?.Change(Timeout.Infinite, Timeout.Infinite);
            _log.Info("Планировщик остановлен");
        }

        private void OnTimerTick(object? state)
        {
            if (!IsEnabled || _isChecking) return;

            _isChecking = true;
            try
            {
                var dueProjects = _projectManager.GetDueProjects();
                if (dueProjects.Count > 0)
                {
                    _log.Info($"Планировщик: {dueProjects.Count} проект(ов) добавлено в очередь");
                    _queueManager.EnqueueProjects(dueProjects);
                }
            }
            catch (Exception ex)
            {
                _log.Error($"Ошибка планировщика: {ex.Message}", details: ex.ToString());
            }
            finally
            {
                _isChecking = false;
            }
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
