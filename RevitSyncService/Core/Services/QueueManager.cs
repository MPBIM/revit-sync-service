using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RevitSyncService.Core.Interfaces;
using RevitSyncService.Core.Models;
using RevitSyncService.Infrastructure.Database;

namespace RevitSyncService.Core.Services
{
    public class QueueManager
    {
        private readonly ProjectManager _projectManager;
        private readonly IDownloadService _downloadService;
        private readonly IConversionService _conversionService;
        private readonly ILogService _log;

        private readonly object _lock = new();
        private readonly List<Project> _queue = new();
        private CancellationTokenSource? _cts;
        private bool _isProcessing;

        // Сколько времени объект считается реально занятым без подтверждения (heartbeat),
        // прежде чем блокировку можно перехватить как "протухшую" (машина упала/зависла).
        private static readonly TimeSpan LockStaleAfter = TimeSpan.FromMinutes(20);

        // Как часто продлевать блокировку во время долгой обработки одного объекта.
        private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromMinutes(3);

        public event Action<ProgressInfo>? OnProgress;
        public event Action? OnCompleted;
        public event Action? OnQueueUpdated;

        public bool IsRunning => _isProcessing;
        private readonly ConfigService _configService;

        /// <summary>
        /// Текущая очередь (копия для UI)
        /// </summary>
        public List<Project> CurrentQueue
        {
            get
            {
                lock (_lock) { return _queue.ToList(); }
            }
        }

        public QueueManager(
            ProjectManager projectManager,
            IDownloadService downloadService,
            IConversionService conversionService,
            ILogService log,
            ConfigService configService)
        {
            _projectManager = projectManager;
            _downloadService = downloadService;
            _conversionService = conversionService;
            _log = log;
            _configService = configService;
        }

        /// <summary>
        /// Добавить проекты в очередь. Если обработка не идёт — запустить.
        /// Используется одинаково и планировщиком (авто-очередь на ВПК),
        /// и кнопкой ручного запуска (БПК) — реальная защита от параллельной
        /// обработки одного и того же объекта находится ниже, в ProcessSingleProjectAsync.
        /// </summary>
        public void EnqueueProjects(List<Project> projects)
        {
            lock (_lock)
            {
                foreach (var project in projects)
                {
                    // Не добавлять дубли
                    if (_queue.Any(p => p.Id == project.Id))
                        continue;

                    project.Status = ProjectStatus.Queued;
                    _queue.Add(project);
                }

                UpdateQueuePositions();
            }

            OnQueueUpdated?.Invoke();

            // Запустить обработку если не идёт
            if (!_isProcessing)
            {
                _ = StartProcessingAsync();
            }
        }

        /// <summary>
        /// Основной цикл обработки очереди
        /// </summary>
        private async Task StartProcessingAsync()
        {
            if (_isProcessing) return;

            _isProcessing = true;
            _cts = new CancellationTokenSource();
            var progress = new Progress<ProgressInfo>(info => OnProgress?.Invoke(info));

            _log.Info("Начало обработки очереди");

            try
            {
                while (true)
                {
                    Project? next;

                    lock (_lock)
                    {
                        next = _queue.FirstOrDefault();
                        if (next == null) break; // Очередь пуста

                        next.Status = ProjectStatus.Running;
                        next.QueuePosition = 0;
                        UpdateQueuePositions();
                    }

                    OnQueueUpdated?.Invoke();
                    _cts.Token.ThrowIfCancellationRequested();

                    await ProcessSingleProjectAsync(next, progress, _cts.Token);

                    // Убрать из очереди после завершения
                    lock (_lock)
                    {
                        _queue.Remove(next);
                        UpdateQueuePositions();
                    }

                    OnQueueUpdated?.Invoke();
                }

                _log.Info("Очередь обработана");
            }
            catch (OperationCanceledException)
            {
                _log.Warning("Обработка отменена пользователем");

                lock (_lock)
                {
                    foreach (var p in _queue)
                    {
                        p.Status = ProjectStatus.Cancelled;
                        p.QueuePosition = 0;
                    }
                    _queue.Clear();
                }
            }
            catch (Exception ex)
            {
                _log.Error($"Критическая ошибка: {ex.Message}", details: ex.ToString());
            }
            finally
            {
                _isProcessing = false;
                OnProgress?.Invoke(new ProgressInfo { IsRunning = false });
                OnCompleted?.Invoke();
                OnQueueUpdated?.Invoke();
                _cts?.Dispose();
                _cts = null;
            }
        }

        /// <summary>
        /// Обработка одного проекта. Единая точка защиты от параллельной выгрузки:
        /// сначала пытаемся атомарно захватить объект в БД. Если не вышло — значит
        /// его прямо сейчас обрабатывает другая машина (ВПК или БПК), и мы просто
        /// пропускаем его, не трогая файлы и не сдвигая расписание.
        /// </summary>
        private async Task ProcessSingleProjectAsync(Project project, IProgress<ProgressInfo>? progress, CancellationToken ct)
        {
            var repo = _configService.Repository;
            string me = MachineIdentity.Current;

            if (repo != null)
            {
                var lockInfo = repo.TryAcquireLock(project.Id, me, LockStaleAfter);
                if (!lockInfo.Acquired)
                {
                    project.Status = ProjectStatus.Waiting;
                    _log.Warning(
                        $"Пропущено: объект уже выгружается ({lockInfo.LockedBy}, с {lockInfo.LockedAt:HH:mm})",
                        project.Name);
                    return;
                }
            }

            project.Status = ProjectStatus.Running;
            _log.Info($"Запуск проекта: {project.Name}", project.Name);

            // Объединённый токен: остановит работу и по кнопке "Отмена",
            // и если блокировку у нас перехватят как протухшую (см. RunHeartbeatLoopAsync).
            using var workCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            Task heartbeatTask = repo == null
                ? Task.CompletedTask
                : RunHeartbeatLoopAsync(repo, project, me, workCts);

            try
            {
                var downloadedFiles = await _downloadService.DownloadFilesAsync(project, progress, workCts.Token);

                if (project.Destination.CreateNwc && downloadedFiles.Count > 0)
                {
                    string revitVersion = project.Source.RevitVersion ?? "2025";
                    int converted = await _conversionService.ConvertToNwcAsync(
                        downloadedFiles, project.Destination.NwcPath, revitVersion, progress, workCts.Token);
                    _log.Info($"Конвертировано: {converted}/{downloadedFiles.Count}", project.Name);
                }

                // Создать задачу на проверку коллизий если конвертация была и NWC папка указана
                if (project.Destination.CreateNwc && !string.IsNullOrEmpty(project.Destination.NwcPath))
                {
                    try
                    {
                        var clashTask = new ClashTask
                        {
                            ProjectId = project.Id,
                            ProjectName = project.Name,
                            NwcFolder = project.Destination.NwcPath,
                            RevitVersion = project.Source.RevitVersion
                        };
                        _configService.Repository?.UpsertClashTaskByProject(clashTask);
                        _log.Info($"Задача на проверку коллизий создана", project.Name);
                    }
                    catch (Exception ex)
                    {
                        // Не падаем — clash task некритичен для основного процесса
                        _log.Warning($"Не удалось создать clash task: {ex.Message}", project.Name);
                    }
                }

                _projectManager.MarkCompleted(project.Id, ProjectStatus.Completed);
                _log.Success($"Проект завершён: {project.Name}", project.Name);
            }
            catch (OperationCanceledException)
            {
                _projectManager.MarkCompleted(project.Id, ProjectStatus.Cancelled);
                _log.Warning($"Проект отменён: {project.Name}", project.Name);
                throw;
            }
            catch (Exception ex)
            {
                _projectManager.MarkCompleted(project.Id, ProjectStatus.Failed);
                _log.Error($"Ошибка: {project.Name}", project.Name, ex.ToString());
            }
            finally
            {
                workCts.Cancel();
                try { await heartbeatTask.ConfigureAwait(false); } catch { /* фоновая, не критично */ }

                if (repo != null)
                    repo.ReleaseLock(project.Id, me);
            }
        }

        /// <summary>
        /// Периодически продлевает блокировку, пока идёт скачивание/конвертация.
        /// Если продление не удалось (кто-то перехватил лок как протухший) —
        /// отменяет workCts, чтобы немедленно остановить скачивание/запись файла.
        /// </summary>
        private static async Task RunHeartbeatLoopAsync(
            DbRepository repo, Project project, string lockedBy, CancellationTokenSource workCts)
        {
            try
            {
                while (!workCts.IsCancellationRequested)
                {
                    await Task.Delay(HeartbeatInterval, workCts.Token).ConfigureAwait(false);

                    bool stillOurs = repo.HeartbeatLock(project.Id, lockedBy);
                    if (!stillOurs)
                    {
                        workCts.Cancel();
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // нормальное завершение при отмене/окончании работы
            }
        }

        /// <summary>
        /// Обновить позиции в очереди
        /// </summary>
        private void UpdateQueuePositions()
        {
            int pos = 1;
            foreach (var p in _queue)
            {
                if (p.Status == ProjectStatus.Queued)
                {
                    p.QueuePosition = pos++;
                }
            }
        }

        /// <summary>
        /// Отменить всю обработку
        /// </summary>
        public void Cancel()
        {
            _cts?.Cancel();
        }

        /// <summary>
        /// Количество в очереди
        /// </summary>
        public int QueueCount
        {
            get { lock (_lock) { return _queue.Count; } }
        }
    }
}