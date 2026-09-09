using System.Windows;
using RevitSyncService.UI.ViewModels;

namespace RevitSyncService.UI.Views
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow(SettingsViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
            vm.RequestClose += result => { DialogResult = result; Close(); };

            // Заполнить PasswordBox из VM
            PasswordBox.Password = vm.Password;
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is SettingsViewModel vm)
                vm.Password = PasswordBox.Password;
        }
    }
}