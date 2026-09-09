using System.Linq;
using System.Windows.Controls;
using RevitSyncService.Core.Models;

namespace RevitSyncService.UI.Views
{
    public partial class ProjectsView : UserControl
    {
        public ProjectsView()
        {
            InitializeComponent();
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is ViewModels.ProjectsViewModel vm)
            {
                vm.SelectedProjects = ProjectsGrid.SelectedItems
                    .Cast<Core.Models.Project>()
                    .ToList();
            }
        }
    }
}