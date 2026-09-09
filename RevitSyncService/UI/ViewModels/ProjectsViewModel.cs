using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using RevitSyncService.Core.Models;
using RevitSyncService.Core.Services;
using RevitSyncService.UI.Commands;

namespace RevitSyncService.UI.ViewModels
{
    public class ProjectsViewModel : BaseViewModel
    {
        private readonly ProjectManager _projectManager;
        private readonly QueueManager _queueManager;

        /// <summary>
        /// Все проекты
        /// </summary>
        public ObservableCollection<Project> Projects { get; } = new();

        /// <summary>
        /// Отфильтрованные проекты (для DataGrid)
        /// </summary>
        public ObservableCollection<Project> FilteredProjects { get; } = new();

        /// <summary>
        /// Текст поиска
        /// </summary>
        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetField(ref _searchText, value))
                    ApplyFilter();
            }
        }

        /// <summary>
        /// Выбранный проект (одиночный, для редактирования/удаления)
        /// </summary>
        private Project? _selectedProject;
        public Project? SelectedProject
        {
            get => _selectedProject;
            set => SetField(ref _selectedProject, value);
        }

        /// <summary>
        /// Список выбранных проектов (мультивыбор)
        /// </summary>
        private List<Project> _selectedProjects = new();
        public List<Project> SelectedProjects
        {
            get => _selectedProjects;
            set
            {
                _selectedProjects = value;
                SelectedProject = value.FirstOrDefault();
                OnPropertyChanged(nameof(SelectionInfo));
            }
        }

        /// <summary>
        /// Инфо о выборе для UI
        /// </summary>
        public string SelectionInfo => _selectedProjects.Count > 1
            ? $"Выбрано: {_selectedProjects.Count}"
            : string.Empty;

        // Progress
        private bool _isRunning;
        public bool IsRunning
        {
            get => _isRunning;
            set => SetField(ref _isRunning, value);
        }

        private double _progressPercentage;
        public double ProgressPercentage
        {
            get => _progressPercentage;
            set => SetField(ref _progressPercentage, value);
        }

        private string _progressText = string.Empty;
        public string ProgressText
        {
            get => _progressText;
            set => SetField(ref _progressText, value);
        }

        private string _currentOperation = string.Empty;
        public string CurrentOperation
        {
            get => _currentOperation;
            set => SetField(ref _currentOperation, value);
        }

        public bool HasNoProjects => FilteredProjects.Count == 0;

        // Commands
        public ICommand CreateCommand { get; }
        public ICommand EditCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand RunSelectedCommand { get; }
        public ICommand CancelCommand { get; }

        public ProjectsViewModel(ProjectManager projectManager, QueueManager queueManager)
        {
            _projectManager = projectManager;
            _queueManager = queueManager;

            CreateCommand = new RelayCommand(OnCreate);
            EditCommand = new RelayCommand(OnEdit, () => SelectedProject != null);
            DeleteCommand = new RelayCommand(OnDelete, () => SelectedProject != null);
            RunSelectedCommand = new RelayCommand(OnRunSelected, () => _selectedProjects.Count > 0);
            CancelCommand = new RelayCommand(OnCancel, () => IsRunning);

            _queueManager.OnProgress += info =>
            {
                Application.Current?.Dispatcher.Invoke(() => UpdateProgress(info));
            };

            _queueManager.OnCompleted += () =>
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    IsRunning = false;
                    RefreshProjects();
                });
            };

            _queueManager.OnQueueUpdated += () =>
            {
                Application.Current?.Dispatcher.Invoke(() => RefreshProjects());
            };

            RefreshProjects();
        }

        public void RefreshProjects()
        {
            Projects.Clear();
            foreach (var p in _projectManager.Projects)
                Projects.Add(p);

            ApplyFilter();
        }

        private void ApplyFilter()
        {
            FilteredProjects.Clear();

            var source = string.IsNullOrWhiteSpace(SearchText)
                ? Projects
                : new ObservableCollection<Project>(
                    Projects.Where(p => p.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)));

            foreach (var p in source)
                FilteredProjects.Add(p);

            OnPropertyChanged(nameof(HasNoProjects));
        }

        private void OnCreate()
        {
            var editor = new Views.ProjectEditorWindow(null, _projectManager);
            if (editor.ShowDialog() == true)
                RefreshProjects();
        }

        private void OnEdit()
        {
            if (SelectedProject == null) return;
            var editor = new Views.ProjectEditorWindow(SelectedProject, _projectManager);
            if (editor.ShowDialog() == true)
                RefreshProjects();
        }

        private void OnDelete()
        {
            if (_selectedProjects.Count == 0) return;

            string msg = _selectedProjects.Count == 1
                ? $"Удалить проект \"{_selectedProjects[0].Name}\"?"
                : $"Удалить {_selectedProjects.Count} проект(ов)?";

            var result = MessageBox.Show(msg, "Подтверждение",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                foreach (var p in _selectedProjects.ToList())
                    _projectManager.Delete(p.Id);
                RefreshProjects();
            }
        }

        private void OnRunSelected()
        {
            if (_selectedProjects.Count == 0) return;
            _queueManager.EnqueueProjects(_selectedProjects.ToList());
        }

        private void OnCancel()
        {
            _queueManager.Cancel();
        }

        private void UpdateProgress(ProgressInfo info)
        {
            IsRunning = info.IsRunning;
            ProgressPercentage = info.Percentage;
            CurrentOperation = info.CurrentOperation;
            ProgressText = info.IsRunning
                ? $"Обработано: {info.ProcessedFiles}/{info.TotalFiles} | Успешно: {info.SuccessCount} | Ошибок: {info.ErrorCount}"
                : string.Empty;
        }
    }
}