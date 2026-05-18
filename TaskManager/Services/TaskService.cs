using System.Data;
using TaskManager.Data;
using TaskManager.Models;

namespace TaskManager.Services
{
    public interface ITaskService
    {
        Task<PagedResult<TaskItem>> GetPagedAsync(TaskFilterViewModel filter, int? currentUserId = null);
        Task<TaskItem?> GetByIdAsync(int id);
        Task<IEnumerable<TaskItem>> GetByProjectIdAsync(int projectId);
        Task<IEnumerable<TaskItem>> GetByAssigneeIdAsync(int assigneeId);
        Task<int> CreateAsync(TaskItem task);
        Task<bool> UpdateAsync(TaskItem task);
        Task<bool> DeleteAsync(int id);
        Task<bool> UpdateStatusAsync(int taskId, string newStatus, int userId);
        Task<bool> AssignUserAsync(int taskId, int? assigneeId, int userId);
        Task<IEnumerable<string>> GetAllStatusesAsync();
        Task<bool> ValidateStatusTransition(string fromStatus, string toStatus);
    }

    public class TaskService : BaseRepository, ITaskService
    {
        // Допустимые переходы статусов
        private static readonly Dictionary<string, string[]> ValidTransitions = new()
        {
            ["К выполнению"] = new[] { "В работе" },
            ["В работе"] = new[] { "На проверке" },
            ["На проверке"] = new[] { "Завершено", "В работе" },
            ["Завершено"] = Array.Empty<string>() // Из завершённого нельзя перейти никуда
        };

        public TaskService(IDbConnectionFactory connectionFactory) : base(connectionFactory) { }

        public async Task<PagedResult<TaskItem>> GetPagedAsync(TaskFilterViewModel filter, int? currentUserId = null)
        {
            var whereClauses = new List<string> { "t.[IsDeleted] = 0" };
            
            // Фильтрация по проекту
            if (filter.ProjectId.HasValue)
            {
                whereClauses.Add("t.[КодПроекта] = @ProjectId");
            }

            // Фильтрация по статусу
            if (!string.IsNullOrEmpty(filter.StatusName))
            {
                whereClauses.Add("t.[НаименованиеСтатуса] = @StatusName");
            }

            // Фильтрация по исполнителю
            if (filter.AssigneeId.HasValue)
            {
                whereClauses.Add("t.[КодИсполнителя] = @AssigneeId");
            }

            // Поиск по названию
            if (!string.IsNullOrEmpty(filter.SearchTerm))
            {
                whereClauses.Add("t.[Название] LIKE @SearchTerm");
            }

            // Ограничение доступа для не-администраторов
            // (реализуется на уровне контроллера или через additional WHERE clause)

            var whereSql = string.Join(" AND ", whereClauses);
            var offset = (filter.PageNumber - 1) * filter.PageSize;

            // SQL для получения данных с пагинацией
            var sql = $@"
                SELECT COUNT(*) OVER() AS [TotalCount],
                       t.[Код], t.[КодПроекта], t.[КодИсполнителя], t.[НаименованиеСтатуса],
                       t.[Название], t.[Описание], t.[ДатаОкончания], t.[Приоритет],
                       t.[CreatedAt], t.[UpdatedAt], t.[IsDeleted],
                       p.[Название] AS [ProjectTitle],
                       u.[Имя] AS [AssigneeName], u.[Email] AS [AssigneeEmail]
                FROM [dbo].[Задача] t
                LEFT JOIN [dbo].[Проект] p ON t.[КодПроекта] = p.[Код]
                LEFT JOIN [dbo].[Пользователь] u ON t.[КодИсполнителя] = u.[Код]
                WHERE {whereSql}
                ORDER BY 
                    CASE t.[Приоритет] WHEN 4 THEN 1 WHEN 3 THEN 2 WHEN 2 THEN 3 WHEN 1 THEN 4 END,
                    t.[CreatedAt] DESC
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

            var results = await QueryAsync(sql, new
            {
                filter.ProjectId,
                filter.StatusName,
                filter.AssigneeId,
                SearchTerm = string.IsNullOrEmpty(filter.SearchTerm) ? null : $"%{filter.SearchTerm}%",
                Offset = offset,
                PageSize = filter.PageSize
            }, reader => new TaskItem
            {
                Id = reader.GetInt32(1),
                ProjectId = reader.GetInt32(2),
                Project = new Project { Title = reader.IsDBNull(14) ? string.Empty : reader.GetString(14) },
                AssigneeId = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                Assignee = reader.IsDBNull(15) ? null : new User
                {
                    Name = reader.GetString(15),
                    Email = reader.IsDBNull(16) ? string.Empty : reader.GetString(16)
                },
                StatusName = reader.GetString(4),
                Title = reader.GetString(5),
                Description = reader.IsDBNull(6) ? null : reader.GetString(6),
                DueDate = reader.IsDBNull(7) ? null : reader.GetDateTime(7),
                Priority = reader.GetInt32(8),
                CreatedAt = reader.GetDateTime(9),
                UpdatedAt = reader.IsDBNull(10) ? null : reader.GetDateTime(10),
                IsDeleted = reader.GetBoolean(11)
            });

            var totalCount = results.FirstOrDefault()?.Id ?? 0;
            
            // Получаем фактический TotalCount из первой записи
            var countSql = $@"SELECT COUNT(*) FROM [dbo].[Задача] t WHERE {whereSql}";
            var total = await ExecuteScalarAsync<int>(countSql, new
            {
                filter.ProjectId,
                filter.StatusName,
                filter.AssigneeId,
                SearchTerm = string.IsNullOrEmpty(filter.SearchTerm) ? null : $"%{filter.SearchTerm}%"
            });

            return new PagedResult<TaskItem>
            {
                Items = results,
                TotalCount = total,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize
            };
        }

        public async Task<TaskItem?> GetByIdAsync(int id)
        {
            const string sql = @"
                SELECT t.[Код], t.[КодПроекта], t.[КодИсполнителя], t.[НаименованиеСтатуса],
                       t.[Название], t.[Описание], t.[ДатаОкончания], t.[Приоритет],
                       t.[CreatedAt], t.[UpdatedAt], t.[IsDeleted],
                       p.[Название] AS [ProjectTitle],
                       u.[Имя] AS [AssigneeName], u.[Email] AS [AssigneeEmail]
                FROM [dbo].[Задача] t
                LEFT JOIN [dbo].[Проект] p ON t.[КодПроекта] = p.[Код]
                LEFT JOIN [dbo].[Пользователь] u ON t.[КодИсполнителя] = u.[Код]
                WHERE t.[Код] = @Id AND t.[IsDeleted] = 0";

            return await QueryFirstOrDefaultAsync(sql, new { Id = id }, reader => new TaskItem
            {
                Id = reader.GetInt32(0),
                ProjectId = reader.GetInt32(1),
                Project = new Project { Title = reader.IsDBNull(11) ? string.Empty : reader.GetString(11) },
                AssigneeId = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                Assignee = reader.IsDBNull(12) ? null : new User
                {
                    Name = reader.GetString(12),
                    Email = reader.IsDBNull(13) ? string.Empty : reader.GetString(13)
                },
                StatusName = reader.GetString(3),
                Title = reader.GetString(4),
                Description = reader.IsDBNull(5) ? null : reader.GetString(5),
                DueDate = reader.IsDBNull(6) ? null : reader.GetDateTime(6),
                Priority = reader.GetInt32(7),
                CreatedAt = reader.GetDateTime(8),
                UpdatedAt = reader.IsDBNull(9) ? null : reader.GetDateTime(9),
                IsDeleted = reader.GetBoolean(10)
            });
        }

        public async Task<IEnumerable<TaskItem>> GetByProjectIdAsync(int projectId)
        {
            const string sql = @"
                SELECT [Код], [КодПроекта], [КодИсполнителя], [НаименованиеСтатуса],
                       [Название], [Описание], [ДатаОкончания], [Приоритет],
                       [CreatedAt], [UpdatedAt], [IsDeleted]
                FROM [dbo].[Задача]
                WHERE [КодПроекта] = @ProjectId AND [IsDeleted] = 0
                ORDER BY [Приоритет] DESC, [CreatedAt] DESC";

            return await QueryAsync(sql, new { ProjectId = projectId }, MapTask);
        }

        public async Task<IEnumerable<TaskItem>> GetByAssigneeIdAsync(int assigneeId)
        {
            const string sql = @"
                SELECT [Код], [КодПроекта], [КодИсполнителя], [НаименованиеСтатуса],
                       [Название], [Описание], [ДатаОкончания], [Приоритет],
                       [CreatedAt], [UpdatedAt], [IsDeleted]
                FROM [dbo].[Задача]
                WHERE [КодИсполнителя] = @AssigneeId AND [IsDeleted] = 0
                ORDER BY [Приоритет] DESC, [ДатаОкончания] ASC";

            return await QueryAsync(sql, new { AssigneeId = assigneeId }, MapTask);
        }

        public async Task<int> CreateAsync(TaskItem task)
        {
            const string sql = @"
                INSERT INTO [dbo].[Задача] ([КодПроекта], [КодИсполнителя], [НаименованиеСтатуса],
                                            [Название], [Описание], [ДатаОкончания], [Приоритет])
                VALUES (@ProjectId, @AssigneeId, @StatusName, @Title, @Description, @DueDate, @Priority);
                SELECT SCOPE_IDENTITY();";

            return await ExecuteScalarAsync<int>(sql, new
            {
                task.ProjectId,
                AssigneeId = task.AssigneeId ?? (object)DBNull.Value,
                task.StatusName,
                task.Title,
                Description = task.Description ?? (object)DBNull.Value,
                DueDate = task.DueDate ?? (object)DBNull.Value,
                task.Priority
            });
        }

        public async Task<bool> UpdateAsync(TaskItem task)
        {
            const string sql = @"
                UPDATE [dbo].[Задача]
                SET [КодПроекта] = @ProjectId,
                    [КодИсполнителя] = @AssigneeId,
                    [НаименованиеСтатуса] = @StatusName,
                    [Название] = @Title,
                    [Описание] = @Description,
                    [ДатаОкончания] = @DueDate,
                    [Приоритет] = @Priority,
                    [UpdatedAt] = GETDATE()
                WHERE [Код] = @Id AND [IsDeleted] = 0";

            var rowsAffected = await ExecuteNonQueryAsync(sql, new
            {
                task.Id,
                task.ProjectId,
                AssigneeId = task.AssigneeId ?? (object)DBNull.Value,
                task.StatusName,
                task.Title,
                Description = task.Description ?? (object)DBNull.Value,
                DueDate = task.DueDate ?? (object)DBNull.Value,
                task.Priority
            });

            return rowsAffected > 0;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            const string sql = @"UPDATE [dbo].[Задача] SET [IsDeleted] = 1, [UpdatedAt] = GETDATE() WHERE [Код] = @Id";
            var rowsAffected = await ExecuteNonQueryAsync(sql, new { Id = id });
            return rowsAffected > 0;
        }

        public async Task<bool> UpdateStatusAsync(int taskId, string newStatus, int userId)
        {
            // Получаем текущий статус
            var task = await GetByIdAsync(taskId);
            if (task == null) return false;

            // Валидация перехода статуса
            if (!await ValidateStatusTransition(task.StatusName, newStatus))
            {
                throw new InvalidOperationException($"Недопустимый переход статуса: {task.StatusName} -> {newStatus}");
            }

            // Проверка: задача не может быть переведена в "В работе" без исполнителя
            if (newStatus == "В работе" && task.AssigneeId == null)
            {
                throw new InvalidOperationException("Задача должна иметь исполнителя для перевода в статус 'В работе'");
            }

            const string sql = @"
                UPDATE [dbo].[Задача]
                SET [НаименованиеСтатуса] = @NewStatus, [UpdatedAt] = GETDATE()
                WHERE [Код] = @Id AND [IsDeleted] = 0";

            var rowsAffected = await ExecuteNonQueryAsync(sql, new { Id = taskId, NewStatus = newStatus });

            if (rowsAffected > 0)
            {
                // Логирование аудита
                await LogAuditAsync(userId, "UpdateStatus", "Задача", taskId, task.StatusName, newStatus);
            }

            return rowsAffected > 0;
        }

        public async Task<bool> AssignUserAsync(int taskId, int? assigneeId, int userId)
        {
            const string sql = @"
                UPDATE [dbo].[Задача]
                SET [КодИсполнителя] = @AssigneeId, [UpdatedAt] = GETDATE()
                WHERE [Код] = @Id AND [IsDeleted] = 0";

            var task = await GetByIdAsync(taskId);
            var oldValue = task?.AssigneeId?.ToString() ?? "null";
            var newValue = assigneeId?.ToString() ?? "null";

            var rowsAffected = await ExecuteNonQueryAsync(sql, new
            {
                Id = taskId,
                AssigneeId = assigneeId ?? (object)DBNull.Value
            });

            if (rowsAffected > 0)
            {
                await LogAuditAsync(userId, "AssignUser", "Задача", taskId, oldValue, newValue);
            }

            return rowsAffected > 0;
        }

        public async Task<IEnumerable<string>> GetAllStatusesAsync()
        {
            const string sql = @"SELECT [Наименование] FROM [dbo].[Статус] ORDER BY [Наименование]";
            return await QueryAsync(sql, null, reader => reader.GetString(0));
        }

        public Task<bool> ValidateStatusTransition(string fromStatus, string toStatus)
        {
            if (!ValidTransitions.ContainsKey(fromStatus))
                return Task.FromResult(false);

            var isValid = ValidTransitions[fromStatus].Contains(toStatus);
            return Task.FromResult(isValid);
        }

        private TaskItem MapTask(IDataReader reader)
        {
            return new TaskItem
            {
                Id = reader.GetInt32(0),
                ProjectId = reader.GetInt32(1),
                AssigneeId = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                StatusName = reader.GetString(3),
                Title = reader.GetString(4),
                Description = reader.IsDBNull(5) ? null : reader.GetString(5),
                DueDate = reader.IsDBNull(6) ? null : reader.GetDateTime(6),
                Priority = reader.GetInt32(7),
                CreatedAt = reader.GetDateTime(8),
                UpdatedAt = reader.IsDBNull(9) ? null : reader.GetDateTime(9),
                IsDeleted = reader.GetBoolean(10)
            };
        }

        private async Task LogAuditAsync(int userId, string action, string entityType, int entityId, string? oldValue, string? newValue)
        {
            const string sql = @"
                INSERT INTO [dbo].[AuditLog] ([UserId], [Action], [EntityType], [EntityId], [OldValue], [NewValue])
                VALUES (@UserId, @Action, @EntityType, @EntityId, @OldValue, @NewValue)";

            await ExecuteNonQueryAsync(sql, new
            {
                UserId = userId,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                OldValue = oldValue ?? (object)DBNull.Value,
                NewValue = newValue ?? (object)DBNull.Value
            });
        }
    }
}
