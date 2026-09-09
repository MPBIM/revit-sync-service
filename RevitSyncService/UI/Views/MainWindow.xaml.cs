using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using RevitSyncService.UI.ViewModels;

namespace RevitSyncService.UI.Views
{
    public partial class MainWindow : Window
    {
        private System.Windows.Forms.NotifyIcon? _trayIcon;

        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            InitializeTrayIcon();
        }

        private void InitializeTrayIcon()
        {
            _trayIcon = new System.Windows.Forms.NotifyIcon
            {
                Text = "RevitSync Service",
                Visible = false
            };

            // Используем стандартную иконку приложения
            try
            {
                var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MPFileExport.ico");
                if (File.Exists(iconPath))
                    _trayIcon.Icon = new System.Drawing.Icon(iconPath);
                else
                    _trayIcon.Icon = System.Drawing.SystemIcons.Application;
            }
            catch
            {
                _trayIcon.Icon = System.Drawing.SystemIcons.Application;
            }

            // Контекстное меню трея
            var menu = new System.Windows.Forms.ContextMenuStrip();
            menu.Items.Add("Открыть", null, (s, e) => ShowFromTray());
            menu.Items.Add("-");
            menu.Items.Add("Выход", null, (s, e) =>
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
                Application.Current.Shutdown();
            });
            _trayIcon.ContextMenuStrip = menu;

            // Двойной клик — развернуть
            _trayIcon.DoubleClick += (s, e) => ShowFromTray();
        }

        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);

            if (WindowState == WindowState.Minimized)
            {
                Hide();
                try
                {
                    if (_trayIcon != null)
                    {
                        _trayIcon.Visible = true;
                        _trayIcon.ShowBalloonTip(2000, "RevitSync Service",
                            "Приложение свёрнуто в трей", System.Windows.Forms.ToolTipIcon.Info);
                    }
                }
                catch { }
            }
        }

        private void ShowFromTray()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
            if (_trayIcon != null)
                _trayIcon.Visible = false;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (_trayIcon == null)
                return;

            e.Cancel = true;
            WindowState = WindowState.Minimized;
        }

        private void OnExitClick(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Завершить работу? Планировщик будет остановлен.",
                "Выход", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // Сначала обнуляем чтобы OnClosing и OnStateChanged не трогали
                var tray = _trayIcon;
                _trayIcon = null;

                try
                {
                    if (tray != null)
                    {
                        tray.Visible = false;
                        tray.Dispose();
                    }
                }
                catch { }

                Application.Current.Shutdown();
            }
        }

        private void OnSettingsClick(object sender, RoutedEventArgs e)
        {
            var mainVm = DataContext as RevitSyncService.UI.ViewModels.MainViewModel;
            if (mainVm == null) return;

            var settingsVm = new RevitSyncService.UI.ViewModels.SettingsViewModel(mainVm.DbConnectionService);
            var settingsWin = new RevitSyncService.UI.Views.SettingsWindow(settingsVm)
            {
                Owner = this
            };

            if (settingsWin.ShowDialog() == true)
            {
                MessageBox.Show("Настройки сохранены. Перезапустите приложение для применения.",
                    "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}