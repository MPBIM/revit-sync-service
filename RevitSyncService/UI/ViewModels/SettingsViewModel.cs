using System;
using System.Windows;
using System.Windows.Input;
using RevitSyncService.Core.Models;
using RevitSyncService.Core.Services;
using RevitSyncService.UI.Commands;

namespace RevitSyncService.UI.ViewModels
{
    public class SettingsViewModel : BaseViewModel
    {
        private readonly DatabaseConnectionService _dbService;

        private string _host = "localhost";
        public string Host { get => _host; set => SetField(ref _host, value); }

        private int _port = 5432;
        public int Port { get => _port; set => SetField(ref _port, value); }

        private string _database = "progress";
        public string Database { get => _database; set => SetField(ref _database, value); }

        private string _username = "progress";
        public string Username { get => _username; set => SetField(ref _username, value); }

        private string _password = "12345678";
        public string Password { get => _password; set => SetField(ref _password, value); }

        private string _testResult = string.Empty;
        public string TestResult { get => _testResult; set => SetField(ref _testResult, value); }

        private bool _testSuccess;
        public bool TestSuccess
        {
            get => _testSuccess;
            set
            {
                SetField(ref _testSuccess, value);
                OnPropertyChanged(nameof(TestResultColor));
            }
        }

        private bool _isTesting;
        public bool IsTesting { get => _isTesting; set => SetField(ref _isTesting, value); }

        public bool IsConnected { get; private set; }

        public ICommand TestCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        public event Action<bool>? RequestClose;
        // Добавить свойство в SettingsViewModel
        public System.Windows.Media.Brush TestResultColor =>
            TestSuccess
                ? System.Windows.Media.Brushes.Green
                : System.Windows.Media.Brushes.OrangeRed;
        public SettingsViewModel(DatabaseConnectionService dbService)
        {
            _dbService = dbService;

            // Заполнить поля из сохранённых настроек
            if (dbService.Config != null)
            {
                Host = dbService.Config.Host;
                Port = dbService.Config.Port;
                Database = dbService.Config.Database;
                Username = dbService.Config.Username;
                Password = dbService.Config.Password;
            }

            TestCommand = new RelayCommand(() => { _ = TestAsync(); });
            SaveCommand = new RelayCommand(Save);
            CancelCommand = new RelayCommand(() => RequestClose?.Invoke(false));
        }

        private async System.Threading.Tasks.Task TestAsync()
        {
            IsTesting = true;
            TestResult = "Проверка соединения...";
            TestSuccess = false;

            var config = BuildConfig();

            await System.Threading.Tasks.Task.Run(async () =>
            {
                bool success;
                string message;

                try
                {
                    var builder = new Npgsql.NpgsqlConnectionStringBuilder
                    {
                        Host = config.Host,
                        Port = config.Port,
                        Database = config.Database,
                        Username = config.Username,
                        Password = config.Password,
                        Timeout = 5,
                        // Отключаем пул соединений для теста
                        Pooling = false
                    };

                    await using var conn = new Npgsql.NpgsqlConnection(builder.ConnectionString);
                    await conn.OpenAsync().ConfigureAwait(false);

                    await using var cmd = new Npgsql.NpgsqlCommand("SELECT version()", conn);
                    var ver = await cmd.ExecuteScalarAsync().ConfigureAwait(false);

                    success = true;
                    message = $"✓ Соединение успешно!\n{ver}";
                }
                catch (Npgsql.PostgresException ex)
                {
                    success = false;
                    // Фикс кодировки кириллицы в сообщениях PostgreSQL
                    string pgMessage = ex.MessageText ?? ex.Message;
                    try
                    {
                        byte[] bytes = System.Text.Encoding.GetEncoding(1252).GetBytes(pgMessage);
                        pgMessage = System.Text.Encoding.GetEncoding(1251).GetString(bytes);
                    }
                    catch { /* оставляем как есть */ }

                    message = $"✗ PostgreSQL [{ex.SqlState}]\n{pgMessage}";
                }
                catch (Exception ex)
                {
                    success = false;
                    message = $"✗ {ex.GetType().Name}:\n{ex.Message}";
                    if (ex.InnerException != null)
                        message += $"\n\nВнутренняя: {ex.InnerException.Message}";
                }

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    TestSuccess = success;
                    TestResult = message;
                    IsTesting = false;

                    // Временно — показываем полный текст ошибки
                    if (!success)
                    {
                        System.Windows.MessageBox.Show(message, "Диагностика подключения",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Information);
                    }
                });
            });
        }
        private void Save()
        {
            var config = BuildConfig();
            _dbService.Save(config);
            IsConnected = true;
            RequestClose?.Invoke(true);
        }

        private DbConnectionConfig BuildConfig() => new()
        {
            Host = Host,
            Port = Port,
            Database = Database,
            Username = Username,
            Password = Password
        };
    }
}