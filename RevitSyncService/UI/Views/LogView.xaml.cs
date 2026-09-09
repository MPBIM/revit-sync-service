using System.Collections.Specialized;
using System.Windows.Controls;

namespace RevitSyncService.UI.Views
{
    public partial class LogView : UserControl
    {
        public LogView()
        {
            InitializeComponent();

            // Автоскролл при добавлении записей
            ((INotifyCollectionChanged)LogListBox.Items).CollectionChanged += (s, e) =>
            {
                if (LogListBox.Items.Count > 0)
                {
                    LogListBox.ScrollIntoView(LogListBox.Items[LogListBox.Items.Count - 1]);
                }
            };
        }
    }
}