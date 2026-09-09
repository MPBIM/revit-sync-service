using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using RevitSyncService.Core.Interfaces;
using RevitSyncService.Core.Models;
using RevitSyncService.UI.Commands;

namespace RevitSyncService.UI.ViewModels
{
    public class LogViewModel : BaseViewModel
    {
        private readonly ILogService _logService;

        public ObservableCollection<LogEntry> LogEntries { get; } = new();

        public ICommand RefreshCommand { get; }
        public ICommand ClearCommand { get; }

        public LogViewModel(ILogService logService)
        {
            _logService = logService;

            RefreshCommand = new RelayCommand(Refresh);
            ClearCommand = new RelayCommand(Clear);

            // Подписка на новые записи
            _logService.OnNewEntry += entry =>
            {
                Application.Current?.Dispatcher.Invoke(() => LogEntries.Add(entry));
            };

            // Загрузить существующие
            Refresh();
        }

        private void Refresh()
        {
            LogEntries.Clear();
            foreach (var entry in _logService.GetEntries())
                LogEntries.Add(entry);
        }

        private void Clear()
        {
            _logService.Clear();
            LogEntries.Clear();
        }
    }
}
