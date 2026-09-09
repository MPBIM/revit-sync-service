using RevitSyncService.Core.Services;

namespace RevitSyncService.UI.ViewModels
{
    public class MainViewModel : BaseViewModel
    {
        public ProjectsViewModel ProjectsVM { get; }
        public LogViewModel LogVM { get; }
        public DatabaseConnectionService DbConnectionService { get; }

        private readonly Scheduler _scheduler;
        private readonly ConfigService _configService;

        private bool _autoExportEnabled = true;
        public bool AutoExportEnabled
        {
            get => _autoExportEnabled;
            set
            {
                if (_autoExportEnabled == value) return;
                _autoExportEnabled = value;
                OnPropertyChanged(nameof(AutoExportEnabled));
                _scheduler.IsEnabled = value;

                _configService.Config.GlobalSettings.AutoExportEnabled = value;
                _configService.SaveLocalSettings();
            }
        }

        public MainViewModel(ProjectsViewModel projectsVm, LogViewModel logVm,
            DatabaseConnectionService dbConnectionService, Scheduler scheduler,
            ConfigService configService)
        {
            ProjectsVM = projectsVm;
            LogVM = logVm;
            DbConnectionService = dbConnectionService;
            _scheduler = scheduler;
            _configService = configService;

            // Загрузить сохранённое состояние с диска
            _configService.LoadLocalSettings();
            _autoExportEnabled = configService.Config.GlobalSettings.AutoExportEnabled;
            _scheduler.IsEnabled = _autoExportEnabled;
        }
    }
}