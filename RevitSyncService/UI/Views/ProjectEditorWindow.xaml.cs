using System.Windows;
using RevitSyncService.Core.Models;
using RevitSyncService.Core.Services;
using RevitSyncService.UI.ViewModels;

namespace RevitSyncService.UI.Views
{
    public partial class ProjectEditorWindow : Window
    {
        public ProjectEditorWindow(Project? existingProject, ProjectManager projectManager)
        {
            InitializeComponent();

            var vm = new ProjectEditorViewModel(existingProject, projectManager);
            vm.RequestClose += result =>
            {
                DialogResult = result;
                Close();
            };

            DataContext = vm;
        }

        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is ViewModels.FolderTreeItem folder && DataContext is ViewModels.ProjectEditorViewModel vm)
            {
                vm.SelectFolder(folder);
            }
        }
    }
}
