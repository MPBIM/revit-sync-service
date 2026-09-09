using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using RevitSyncService.Core.Models;
using RevitSyncService.Core.Services;
using RevitSyncService.Infrastructure.RevitServer;
using RevitSyncService.UI.Commands;

namespace RevitSyncService.UI.ViewModels
{
    public class ProjectEditorViewModel : BaseViewModel
    {
        private readonly ProjectManager _projectManager;
        private readonly bool _isEditMode;

        // === Основные поля ===
        private string _projectId = string.Empty;

        private string _name = string.Empty;
        public string Name
        {
            get => _name;
            set => SetField(ref _name, value);
        }

        // === Тип источника ===
        private string _sourceType = "NetworkFolder";
        public string SourceType
        {
            get => _sourceType;
            set
            {
                if (SetField(ref _sourceType, value))
                {
                    OnPropertyChanged(nameof(IsRevitServer));
                    OnPropertyChanged(nameof(IsNetworkFolder));
                }
            }
        }

        public bool IsRevitServer => SourceType == "RevitServer";
        public bool IsNetworkFolder => SourceType == "NetworkFolder";

        public string[] SourceTypes { get; } = { "RevitServer", "NetworkFolder" };
        public string[] SourceTypeDisplayNames { get; } = { "Revit Server", "Сетевая папка" };

        // === Revit Server настройки ===
        public ObservableCollection<RevitServerInfo> AvailableServers { get; } = new();

        private RevitServerInfo? _selectedServer;
        public RevitServerInfo? SelectedServer
        {
            get => _selectedServer;
            set
            {
                if (SetField(ref _selectedServer, value) && value != null)
                {
                    SelectedRevitVersion = value.RevitVersion;
                }
            }
        }

        private string _selectedRevitVersion = "2025";
        public string SelectedRevitVersion
        {
            get => _selectedRevitVersion;
            set => SetField(ref _selectedRevitVersion, value);
        }

        public string[] RevitVersions { get; } = { "2023", "2024", "2025", "2026" };

        private string _serverFolderPath = string.Empty;
        public string ServerFolderPath
        {
            get => _serverFolderPath;
            set => SetField(ref _serverFolderPath, value);
        }

        // Дерево папок для Revit Server
        public ObservableCollection<FolderTreeItem> FolderTree { get; } = new();

        private bool _isBrowsingFolders;
        public bool IsBrowsingFolders
        {
            get => _isBrowsingFolders;
            set => SetField(ref _isBrowsingFolders, value);
        }

        // === Сетевая папка ===
        private string _networkFolderPath = string.Empty;
        public string NetworkFolderPath
        {
            get => _networkFolderPath;
            set => SetField(ref _networkFolderPath, value);
        }

        // === Целевые папки ===
        private string _rvtPath = string.Empty;
        public string RvtPath
        {
            get => _rvtPath;
            set => SetField(ref _rvtPath, value);
        }

        private string _nwcPath = string.Empty;
        public string NwcPath
        {
            get => _nwcPath;
            set => SetField(ref _nwcPath, value);
        }

        private bool _createNwc = true;
        public bool CreateNwc
        {
            get => _createNwc;
            set => SetField(ref _createNwc, value);
        }

        // === Расписание ===
        private string _scheduleType = "Weekly";
        public string ScheduleType
        {
            get => _scheduleType;
            set => SetField(ref _scheduleType, value);
        }

        public string[] ScheduleTypes { get; } = { "Weekly", "Biweekly" };
        public string[] ScheduleTypeDisplayNames { get; } = { "Еженедельно", "Раз в 2 недели" };

        private int _dayOfWeek = 0;
        public int DayOfWeek
        {
            get => _dayOfWeek;
            set => SetField(ref _dayOfWeek, value);
        }

        public string[] DayNames { get; } = { "Понедельник", "Вторник", "Среда", "Четверг", "Пятница", "Суббота", "Воскресенье" };

        private string _time = "10:00";
        public string Time
        {
            get => _time;
            set => SetField(ref _time, value);
        }

        // === Исключённые папки ===
        private string _excludedFolders = "Прочее";
        public string ExcludedFolders
        {
            get => _excludedFolders;
            set => SetField(ref _excludedFolders, value);
        }

        // === Заголовок окна ===
        public string WindowTitle => _isEditMode ? "Редактирование проекта" : "Создание проекта";

        // === Команды ===
        public ICommand BrowseServerFoldersCommand { get; }
        public ICommand BrowseNetworkFolderCommand { get; }
        public ICommand BrowseRvtPathCommand { get; }
        public ICommand BrowseNwcPathCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        /// <summary>
        /// Событие для закрытия окна (DialogResult)
        /// </summary>
        public event Action<bool>? RequestClose;

        public ProjectEditorViewModel(Project? existingProject, ProjectManager projectManager)
        {
            _projectManager = projectManager;
            _isEditMode = existingProject != null;

            BrowseServerFoldersCommand = new RelayCommand(async () => await BrowseServerFoldersAsync());
            BrowseNetworkFolderCommand = new RelayCommand(BrowseNetworkFolder);
            BrowseRvtPathCommand = new RelayCommand(BrowseRvtPath);
            BrowseNwcPathCommand = new RelayCommand(BrowseNwcPath);
            SaveCommand = new RelayCommand(Save);
            CancelCommand = new RelayCommand(() => RequestClose?.Invoke(false));

            // Загрузить список серверов
            LoadAvailableServers();

            // Если редактирование — заполнить поля
            if (existingProject != null)
            {
                LoadFromProject(existingProject);
            }
        }

        private void LoadAvailableServers()
        {
            var servers = RsnIniParser.DiscoverServers();
            foreach (var s in servers)
                AvailableServers.Add(s);
        }

        private void LoadFromProject(Project p)
        {
            _projectId = p.Id;
            Name = p.Name;
            SourceType = p.Source.Type;
            SelectedRevitVersion = p.Source.RevitVersion ?? "2025";
            ServerFolderPath = p.Source.IsRevitServer ? p.Source.FolderPath : string.Empty;
            NetworkFolderPath = !p.Source.IsRevitServer ? p.Source.FolderPath : string.Empty;
            RvtPath = p.Destination.RvtPath;
            NwcPath = p.Destination.NwcPath;
            CreateNwc = p.Destination.CreateNwc;
            ScheduleType = p.Schedule.Type;
            DayOfWeek = p.Schedule.DayOfWeek;
            Time = p.Schedule.Time;
            ExcludedFolders = string.Join(", ", p.Source.ExcludedFolders);

            // Выбрать сервер
            if (p.Source.IsRevitServer && !string.IsNullOrEmpty(p.Source.Server))
            {
                SelectedServer = AvailableServers.FirstOrDefault(s => s.Host == p.Source.Server);
            }
        }

        private async Task BrowseServerFoldersAsync()
        {
            if (SelectedServer == null)
            {
                MessageBox.Show("Сначала выберите сервер.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsBrowsingFolders = true;
            FolderTree.Clear();

            try
            {
                using var api = new RevitServerApiService(SelectedServer.Host, SelectedRevitVersion);
                var contents = await api.GetRootContentsAsync();

                foreach (var folder in contents.Folders)
                {
                    var item = CreateFolderItem(folder.Name, "/" + folder.Name, folder.HasContents);
                    FolderTree.Add(item);
                }

                if (FolderTree.Count == 0)
                {
                    MessageBox.Show("Папки на сервере не найдены.", "Информация",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка получения папок: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBrowsingFolders = false;
            }
        }

        private FolderTreeItem CreateFolderItem(string name, string path, bool hasContents)
        {
            var item = new FolderTreeItem
            {
                Name = name,
                Path = path,
                HasContents = hasContents
            };
            item.AddLoadingPlaceholder();
            item.OnExpandRequested += async (sender) => await LoadChildFoldersAsync(sender);
            return item;
        }

        private async Task LoadChildFoldersAsync(FolderTreeItem parent)
        {
            if (parent.IsLoaded || SelectedServer == null) return;
            parent.IsLoaded = true;

            try
            {
                using var api = new RevitServerApiService(SelectedServer.Host, SelectedRevitVersion);
                var contents = await api.GetFolderContentsAsync(parent.Path);

                parent.Children.Clear();

                foreach (var folder in contents.Folders)
                {
                    var child = CreateFolderItem(folder.Name, parent.Path + "/" + folder.Name, folder.HasContents);
                    parent.Children.Add(child);
                }

                // Показать модели как информацию (не раскрываемые)
                foreach (var model in contents.Models)
                {
                    parent.Children.Add(new FolderTreeItem
                    {
                        Name = $"📄 {model.Name}",
                        Path = parent.Path + "/" + model.Name,
                        HasContents = false
                    });
                }

                if (parent.Children.Count == 0)
                {
                    parent.Children.Add(new FolderTreeItem { Name = "(пусто)" });
                }
            }
            catch (Exception)
            {
                parent.Children.Clear();
                parent.Children.Add(new FolderTreeItem { Name = "⚠ Ошибка загрузки" });
            }
        }
        private void BrowseNetworkFolder()
        {
            using var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Выберите папку с RVT файлами",
                ShowNewFolderButton = false
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                NetworkFolderPath = dialog.SelectedPath;
        }

        private void BrowseRvtPath()
        {
            using var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Папка для сохранения RVT файлов",
                ShowNewFolderButton = true
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                RvtPath = dialog.SelectedPath;
        }

        private void BrowseNwcPath()
        {
            using var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Папка для сохранения NWC файлов",
                ShowNewFolderButton = true
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                NwcPath = dialog.SelectedPath;
        }

        private void Save()
        {
            // Валидация
            if (string.IsNullOrWhiteSpace(Name))
            {
                MessageBox.Show("Укажите название проекта.", "Валидация", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string folderPath = IsRevitServer ? ServerFolderPath : NetworkFolderPath;
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                MessageBox.Show("Укажите путь к папке источника.", "Валидация", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(RvtPath))
            {
                MessageBox.Show("Укажите папку для RVT файлов.", "Валидация", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (CreateNwc && string.IsNullOrWhiteSpace(NwcPath))
            {
                MessageBox.Show("Укажите папку для NWC файлов.", "Валидация", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (IsRevitServer && SelectedServer == null)
            {
                MessageBox.Show("Выберите сервер.", "Валидация", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Собираем модель
            var excludedList = ExcludedFolders
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            var project = new Project
            {
                Id = _isEditMode ? _projectId : Guid.NewGuid().ToString(),
                Name = Name,
                Enabled = true,
                Source = new SourceConfig
                {
                    Type = SourceType,
                    Server = IsRevitServer ? SelectedServer?.Host : null,
                    RevitVersion = IsRevitServer ? SelectedRevitVersion : null,
                    FolderPath = folderPath,
                    ExcludedFolders = excludedList
                },
                Destination = new DestinationConfig
                {
                    RvtPath = RvtPath,
                    NwcPath = NwcPath,
                    CreateNwc = CreateNwc
                },
                Schedule = new ScheduleConfig
                {
                    Type = ScheduleType,
                    DayOfWeek = DayOfWeek,
                    Time = Time
                }
            };

            if (_isEditMode)
                _projectManager.Update(project);
            else
                _projectManager.Add(project);

            RequestClose?.Invoke(true);
        }

        /// <summary>
        /// Выбрать папку из дерева
        /// </summary>
        public void SelectFolder(FolderTreeItem folder)
        {
            ServerFolderPath = folder.Path;
        }
    }

    /// <summary>
    /// Элемент дерева папок Revit Server
    /// </summary>
    public class FolderTreeItem : BaseViewModel
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public bool HasContents { get; set; }
        public ObservableCollection<FolderTreeItem> Children { get; } = new();

        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (SetField(ref _isExpanded, value) && value)
                {
                    // Триггерим загрузку при раскрытии
                    OnExpandRequested?.Invoke(this);
                }
            }
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetField(ref _isSelected, value);
        }

        private bool _isLoaded;
        public bool IsLoaded
        {
            get => _isLoaded;
            set => SetField(ref _isLoaded, value);
        }

        /// <summary>
        /// Событие запроса загрузки дочерних папок
        /// </summary>
        public event Action<FolderTreeItem>? OnExpandRequested;

        /// <summary>
        /// Добавить заглушку "Загрузка..." если есть содержимое
        /// </summary>
        public void AddLoadingPlaceholder()
        {
            if (HasContents && Children.Count == 0)
            {
                Children.Add(new FolderTreeItem { Name = "Загрузка..." });
            }
        }
    }
}
