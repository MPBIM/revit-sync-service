using System;
using System.IO;
using System.Windows;
using RevitSyncService.Core.Interfaces;
using RevitSyncService.Core.Services;
using RevitSyncService.Infrastructure.FileSystem;
using RevitSyncService.UI.ViewModels;
using RevitSyncService.UI.Views;

namespace RevitSyncService
{
    public partial class App : Application
    {
        private Scheduler? _scheduler;

        protected override void OnStartup(StartupEventArgs e)
        {
            DispatcherUnhandledException += (s, ex) =>
            {
                string msg = $"{ex.Exception.Message}\n\n" +
                             $"Inner: {ex.Exception.InnerException?.Message}\n\n" +
                             $"Stack:\n{ex.Exception.StackTrace}";

                // Пишем в файл рядом с exe — удобно для диагностики
                File.WriteAllText("error.log", msg);
                MessageBox.Show(msg, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                ex.Handled = true;
            };

            base.OnStartup(e);

            try
            {
                // 1. Загрузить сохранённые настройки подключения
                var dbConnectionService = new DatabaseConnectionService();
                var configService = new ConfigService();

                bool hasConnection = dbConnectionService.Load();

                if (!hasConnection || !configService.Initialize(dbConnectionService.Config?.ToConnectionString()))
                {
                    var settingsVm = new SettingsViewModel(dbConnectionService);
                    var settingsWin = new SettingsWindow(settingsVm);
                    settingsWin.ShowDialog();

                    if (!settingsVm.IsConnected || !configService.Initialize(dbConnectionService.Config?.ToConnectionString()))
                    {
                        MessageBox.Show("Не удалось подключиться к базе данных. Приложение будет закрыто.",
                            "Ошибка подключения", MessageBoxButton.OK, MessageBoxImage.Error);
                        Shutdown();
                        return;
                    }
                }

                // 2. Создание сервисов
                ILogService logService = new LogService(configService.GetExpandedLogFolder(),
                    configService.Config.GlobalSettings.LogRetentionDays);

                var projectManager = new ProjectManager(configService);
                IDownloadService downloadService = new DownloadService(logService);
                IConversionService conversionService = new ConversionService(logService);
                var queueManager = new QueueManager(projectManager, downloadService, conversionService, logService, configService);

                // 3. Планировщик
                _scheduler = new Scheduler(projectManager, queueManager, logService);
                _scheduler.Start();

                // 4. ViewModels
                var projectsVm = new ProjectsViewModel(projectManager, queueManager);
                var logVm = new LogViewModel(logService);
                var mainVm = new MainViewModel(projectsVm, logVm, dbConnectionService, _scheduler, configService);

                // 5. Главное окно
                var mainWindow = new MainWindow(mainVm);
                mainWindow.Show();

                logService.Info("Приложение запущено");
                logService.Info($"БД: {dbConnectionService.Config?.Host}:{dbConnectionService.Config?.Port}/{dbConnectionService.Config?.Database}");
                logService.Info($"Проектов: {projectManager.Projects.Count}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка запуска приложения:\n\n{ex.Message}",
                    "Критическая ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _scheduler?.Stop();
            _scheduler?.Dispose();
            base.OnExit(e);
        }
    }
}