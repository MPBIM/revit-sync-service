using System;
using System.Collections.Generic;
using System.Linq;
using RevitSyncService.Core.Models;

namespace RevitSyncService.Core.Services
{
    public class ProjectManager
    {
        private readonly ConfigService _configService;

        public ProjectManager(ConfigService configService)
        {
            _configService = configService;
        }

        public List<Project> Projects => _configService.Config.Projects;
        public List<Project> EnabledProjects => Projects.Where(p => p.Enabled).ToList();

        public void Add(Project project)
        {
            project.Id = Guid.NewGuid().ToString();
            project.CreatedAt = DateTime.Now;
            project.UpdatedAt = DateTime.Now;
            project.NextRun = project.Schedule.GetNextRunTime();

            Projects.Add(project);
            _configService.Repository?.UpsertProject(project);
        }

        public void Update(Project project)
        {
            var existing = Projects.FirstOrDefault(p => p.Id == project.Id);
            if (existing == null) return;

            int index = Projects.IndexOf(existing);
            project.UpdatedAt = DateTime.Now;
            project.NextRun = project.Schedule.GetNextRunTime(project.LastRun);
            Projects[index] = project;

            _configService.Repository?.UpsertProject(project);
        }

        public void Delete(string projectId)
        {
            var project = Projects.FirstOrDefault(p => p.Id == projectId);
            if (project != null)
            {
                Projects.Remove(project);
                _configService.Repository?.DeleteProject(projectId);
            }
        }

        public void ToggleEnabled(string projectId)
        {
            var project = Projects.FirstOrDefault(p => p.Id == projectId);
            if (project != null)
            {
                project.Enabled = !project.Enabled;
                project.UpdatedAt = DateTime.Now;
                _configService.Repository?.UpsertProject(project);
            }
        }

        public Project? GetById(string projectId)
            => Projects.FirstOrDefault(p => p.Id == projectId);

        public List<Project> GetDueProjects()
            => EnabledProjects
                .Where(p => p.Status != ProjectStatus.Running &&
                            p.Schedule.IsDueNow(p.LastRun, p.NextRun))
                .OrderBy(p => p.Name)
                .ToList();

        public void MarkCompleted(string projectId, ProjectStatus status)
        {
            var project = Projects.FirstOrDefault(p => p.Id == projectId);
            if (project == null) return;

            project.Status = status;
            project.LastRun = DateTime.Now;
            project.NextRun = project.Schedule.GetNextRunTime(project.LastRun);
            project.UpdatedAt = DateTime.Now;

            _configService.Repository?.UpsertProject(project);
        }
    }
}