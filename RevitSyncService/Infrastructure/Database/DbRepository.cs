using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Npgsql;
using RevitSyncService.Core.Models;

namespace RevitSyncService.Infrastructure.Database
{
    public class DbRepository
    {
        private readonly string _connectionString;

        public DbRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task EnsureSchemaAsync()
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync().ConfigureAwait(false);

            await using var cmd = new NpgsqlCommand(@"
                CREATE TABLE IF NOT EXISTS global_settings (
                    id INTEGER PRIMARY KEY DEFAULT 1,
                    excluded_folders TEXT NOT NULL DEFAULT 'Прочее,Archive,Backup',
                    temp_folder TEXT NOT NULL DEFAULT '%AppData%\RevitSyncService\Temp',
                    log_folder TEXT NOT NULL DEFAULT '%AppData%\RevitSyncService\Logs',
                    log_retention_days INTEGER NOT NULL DEFAULT 30,
                    updated_at TIMESTAMP DEFAULT NOW()
                );

                INSERT INTO global_settings (id)
                VALUES (1)
                ON CONFLICT (id) DO NOTHING;

                CREATE TABLE IF NOT EXISTS clash_tasks (
                    id TEXT PRIMARY KEY,
                    project_id TEXT NOT NULL,
                    project_name TEXT NOT NULL,
                    nwc_folder TEXT NOT NULL,
                    status TEXT NOT NULL DEFAULT 'Pending',
                    error_message TEXT,
                    revit_version TEXT,
                    created_at TIMESTAMP DEFAULT NOW(),
                    updated_at TIMESTAMP DEFAULT NOW()
                );

                -- Уникальный индекс по project_id для upsert по проекту
                CREATE UNIQUE INDEX IF NOT EXISTS uix_clash_tasks_project_id
                    ON clash_tasks(project_id);

                CREATE TABLE IF NOT EXISTS projects (
                    id TEXT PRIMARY KEY,
                    name TEXT NOT NULL,
                    enabled BOOLEAN NOT NULL DEFAULT TRUE,

                    source_type TEXT NOT NULL DEFAULT 'NetworkFolder',
                    source_server TEXT,
                    source_revit_version TEXT,
                    source_folder_path TEXT NOT NULL DEFAULT '',
                    source_excluded_folders TEXT NOT NULL DEFAULT 'Прочее',

                    dest_rvt_path TEXT NOT NULL DEFAULT '',
                    dest_nwc_path TEXT NOT NULL DEFAULT '',
                    dest_create_nwc BOOLEAN NOT NULL DEFAULT TRUE,

                    schedule_type TEXT NOT NULL DEFAULT 'Weekly',
                    schedule_day_of_week INTEGER NOT NULL DEFAULT 0,
                    schedule_time TEXT NOT NULL DEFAULT '10:00',

                    last_run TIMESTAMP,
                    next_run TIMESTAMP,
                    status TEXT NOT NULL DEFAULT 'Waiting',

                    created_at TIMESTAMP DEFAULT NOW(),
                    updated_at TIMESTAMP DEFAULT NOW()
                );
            ", conn);

            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        // === GLOBAL SETTINGS ===

        public async Task<GlobalSettings> LoadGlobalSettingsAsync()
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync().ConfigureAwait(false);
            await using var cmd = new NpgsqlCommand(
                "SELECT excluded_folders, temp_folder, log_folder, log_retention_days FROM global_settings WHERE id = 1", conn);

            await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
            if (await reader.ReadAsync().ConfigureAwait(false))
            {
                return new GlobalSettings
                {
                    ExcludedFolders = reader.GetString(0)
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .ToList(),
                    TempFolder = reader.GetString(1),
                    LogFolder = reader.GetString(2),
                    LogRetentionDays = reader.GetInt32(3)
                };
            }
            return new GlobalSettings();
        }

        public async Task SaveGlobalSettingsAsync(GlobalSettings settings)
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync().ConfigureAwait(false);
            await using var cmd = new NpgsqlCommand(@"
                UPDATE global_settings SET
                    excluded_folders = @excluded,
                    temp_folder = @temp,
                    log_folder = @log,
                    log_retention_days = @retention,
                    updated_at = NOW()
                WHERE id = 1", conn);

            cmd.Parameters.AddWithValue("excluded", string.Join(",", settings.ExcludedFolders));
            cmd.Parameters.AddWithValue("temp", settings.TempFolder);
            cmd.Parameters.AddWithValue("log", settings.LogFolder);
            cmd.Parameters.AddWithValue("retention", settings.LogRetentionDays);
            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        // === PROJECTS ===

        public async Task<List<Project>> LoadProjectsAsync()
        {
            var projects = new List<Project>();

            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync().ConfigureAwait(false);
            await using var cmd = new NpgsqlCommand("SELECT * FROM projects ORDER BY created_at", conn);
            await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);

            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                projects.Add(ReadProject(reader));
            }

            return projects;
        }

        public async Task UpsertProjectAsync(Project p)
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync().ConfigureAwait(false);
            await using var cmd = new NpgsqlCommand(@"
                INSERT INTO projects (
                    id, name, enabled,
                    source_type, source_server, source_revit_version,
                    source_folder_path, source_excluded_folders,
                    dest_rvt_path, dest_nwc_path, dest_create_nwc,
                    schedule_type, schedule_day_of_week, schedule_time,
                    last_run, next_run, status,
                    created_at, updated_at
                ) VALUES (
                    @id, @name, @enabled,
                    @src_type, @src_server, @src_version,
                    @src_path, @src_excluded,
                    @dest_rvt, @dest_nwc, @dest_create_nwc,
                    @sch_type, @sch_day, @sch_time,
                    @last_run, @next_run, @status,
                    @created_at, NOW()
                )
                ON CONFLICT (id) DO UPDATE SET
                    name = @name,
                    enabled = @enabled,
                    source_type = @src_type,
                    source_server = @src_server,
                    source_revit_version = @src_version,
                    source_folder_path = @src_path,
                    source_excluded_folders = @src_excluded,
                    dest_rvt_path = @dest_rvt,
                    dest_nwc_path = @dest_nwc,
                    dest_create_nwc = @dest_create_nwc,
                    schedule_type = @sch_type,
                    schedule_day_of_week = @sch_day,
                    schedule_time = @sch_time,
                    last_run = @last_run,
                    next_run = @next_run,
                    status = @status,
                    updated_at = NOW()", conn);

            cmd.Parameters.AddWithValue("id", p.Id);
            cmd.Parameters.AddWithValue("name", p.Name);
            cmd.Parameters.AddWithValue("enabled", p.Enabled);
            cmd.Parameters.AddWithValue("src_type", p.Source.Type);
            cmd.Parameters.AddWithValue("src_server", (object?)p.Source.Server ?? DBNull.Value);
            cmd.Parameters.AddWithValue("src_version", (object?)p.Source.RevitVersion ?? DBNull.Value);
            cmd.Parameters.AddWithValue("src_path", p.Source.FolderPath);
            cmd.Parameters.AddWithValue("src_excluded", string.Join(",", p.Source.ExcludedFolders));
            cmd.Parameters.AddWithValue("dest_rvt", p.Destination.RvtPath);
            cmd.Parameters.AddWithValue("dest_nwc", p.Destination.NwcPath);
            cmd.Parameters.AddWithValue("dest_create_nwc", p.Destination.CreateNwc);
            cmd.Parameters.AddWithValue("sch_type", p.Schedule.Type);
            cmd.Parameters.AddWithValue("sch_day", p.Schedule.DayOfWeek);
            cmd.Parameters.AddWithValue("sch_time", p.Schedule.Time);
            cmd.Parameters.AddWithValue("last_run", (object?)p.LastRun ?? DBNull.Value);
            cmd.Parameters.AddWithValue("next_run", (object?)p.NextRun ?? DBNull.Value);
            cmd.Parameters.AddWithValue("status", p.Status.ToString());
            cmd.Parameters.AddWithValue("created_at", p.CreatedAt);

            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        public async Task DeleteProjectAsync(string projectId)
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync().ConfigureAwait(false);
            await using var cmd = new NpgsqlCommand(
                "DELETE FROM projects WHERE id = @id", conn);
            cmd.Parameters.AddWithValue("id", projectId);
            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        private static Project ReadProject(NpgsqlDataReader r)
        {
            var excludedFolders = r["source_excluded_folders"].ToString()!
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            return new Project
            {
                Id = r["id"].ToString()!,
                Name = r["name"].ToString()!,
                Enabled = (bool)r["enabled"],
                Source = new SourceConfig
                {
                    Type = r["source_type"].ToString()!,
                    Server = r["source_server"] as string,
                    RevitVersion = r["source_revit_version"] as string,
                    FolderPath = r["source_folder_path"].ToString()!,
                    ExcludedFolders = new List<string>(excludedFolders)
                },
                Destination = new DestinationConfig
                {
                    RvtPath = r["dest_rvt_path"].ToString()!,
                    NwcPath = r["dest_nwc_path"].ToString()!,
                    CreateNwc = (bool)r["dest_create_nwc"]
                },
                Schedule = new ScheduleConfig
                {
                    Type = r["schedule_type"].ToString()!,
                    DayOfWeek = (int)r["schedule_day_of_week"],
                    Time = r["schedule_time"].ToString()!
                },
                LastRun = r["last_run"] as DateTime?,
                NextRun = r["next_run"] as DateTime?,
                Status = Enum.TryParse<ProjectStatus>(r["status"].ToString(), out var s) ? s : ProjectStatus.Waiting,
                CreatedAt = (DateTime)r["created_at"],
                UpdatedAt = (DateTime)r["updated_at"]
            };
        }

        // === CLASH TASKS ===

        /// <summary>
        /// Upsert по project_id: если запись есть — сбрасывает статус в Pending.
        /// Используется из QueueManager после каждой успешной конвертации.
        /// </summary>
        public async Task UpsertClashTaskByProjectAsync(ClashTask task)
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync().ConfigureAwait(false);
            await using var cmd = new NpgsqlCommand(@"
                INSERT INTO clash_tasks
                    (id, project_id, project_name, nwc_folder, status, revit_version, created_at, updated_at)
                VALUES
                    (@id, @project_id, @project_name, @nwc_folder, 'Pending', @revit_version, NOW(), NOW())
                ON CONFLICT (project_id) DO UPDATE SET
                    nwc_folder    = EXCLUDED.nwc_folder,
                    status        = 'Pending',
                    revit_version = EXCLUDED.revit_version,
                    error_message = NULL,
                    updated_at    = NOW()", conn);

            cmd.Parameters.AddWithValue("id", task.Id);
            cmd.Parameters.AddWithValue("project_id", task.ProjectId);
            cmd.Parameters.AddWithValue("project_name", task.ProjectName);
            cmd.Parameters.AddWithValue("nwc_folder", task.NwcFolder);
            cmd.Parameters.AddWithValue("revit_version", (object?)task.RevitVersion ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        public async Task UpdateClashTaskStatusAsync(string taskId, ClashTaskStatus status, string? errorMessage = null)
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync().ConfigureAwait(false);
            await using var cmd = new NpgsqlCommand(@"
                UPDATE clash_tasks SET
                    status = @status,
                    error_message = @error,
                    updated_at = NOW()
                WHERE id = @id", conn);

            cmd.Parameters.AddWithValue("id", taskId);
            cmd.Parameters.AddWithValue("status", status.ToString());
            cmd.Parameters.AddWithValue("error", (object?)errorMessage ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        public async Task<List<ClashTask>> GetPendingClashTasksAsync()
        {
            var tasks = new List<ClashTask>();
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync().ConfigureAwait(false);
            await using var cmd = new NpgsqlCommand(
                "SELECT * FROM clash_tasks WHERE status = 'Pending' ORDER BY created_at", conn);
            await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
                tasks.Add(ReadClashTask(reader));
            return tasks;
        }

        private static ClashTask ReadClashTask(NpgsqlDataReader r) => new()
        {
            Id = r["id"].ToString()!,
            ProjectId = r["project_id"].ToString()!,
            ProjectName = r["project_name"].ToString()!,
            NwcFolder = r["nwc_folder"].ToString()!,
            Status = Enum.TryParse<ClashTaskStatus>(r["status"].ToString(), out var s) ? s : ClashTaskStatus.Pending,
            ErrorMessage = r["error_message"] as string,
            RevitVersion = r["revit_version"] as string,
            CreatedAt = (DateTime)r["created_at"],
            UpdatedAt = (DateTime)r["updated_at"]
        };

        // === СИНХРОННЫЕ ОБЁРТКИ ===

        public void UpsertClashTaskByProject(ClashTask task)
            => Task.Run(() => UpsertClashTaskByProjectAsync(task)).GetAwaiter().GetResult();

        public List<ClashTask> GetPendingClashTasks()
            => Task.Run(GetPendingClashTasksAsync).GetAwaiter().GetResult();

        public void UpdateClashTaskStatus(string taskId, ClashTaskStatus status, string? errorMessage = null)
            => Task.Run(() => UpdateClashTaskStatusAsync(taskId, status, errorMessage)).GetAwaiter().GetResult();

        public List<Project> LoadProjects()
            => Task.Run(LoadProjectsAsync).GetAwaiter().GetResult();

        public void UpsertProject(Project p)
            => Task.Run(() => UpsertProjectAsync(p)).GetAwaiter().GetResult();

        public void DeleteProject(string projectId)
            => Task.Run(() => DeleteProjectAsync(projectId)).GetAwaiter().GetResult();

        public GlobalSettings LoadGlobalSettings()
            => Task.Run(LoadGlobalSettingsAsync).GetAwaiter().GetResult();

        public void SaveGlobalSettings(GlobalSettings settings)
            => Task.Run(() => SaveGlobalSettingsAsync(settings)).GetAwaiter().GetResult();
    }
}