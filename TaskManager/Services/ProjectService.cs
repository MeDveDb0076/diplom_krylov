using System.Data;
using TaskManager.Data;
using TaskManager.Models;

namespace TaskManager.Services
{
    public interface IProjectService
    {
        Task<PagedResult<Project>> GetPagedAsync(int pageNumber, int pageSize, string? searchTerm = null);
        Task<Project?> GetByIdAsync(int id);
        Task<IEnumerable<Project>> GetAllAsync();
        Task<IEnumerable<Project>> GetByManagerIdAsync(int managerId);
        Task<int> CreateAsync(Project project);
        Task<bool> UpdateAsync(Project project);
        Task<bool> DeleteAsync(int id);
        Task<IEnumerable<User>> GetTeamMembersAsync(int projectId);
    }

    public class ProjectService : BaseRepository, IProjectService
    {
        public ProjectService(IDbConnectionFactory connectionFactory) : base(connectionFactory) { }

        public async Task<PagedResult<Project>> GetPagedAsync(int pageNumber, int pageSize, string? searchTerm = null)
        {
            var whereClauses = new List<string> { "p.[IsDeleted] = 0" };
            
            if (!string.IsNullOrEmpty(searchTerm))
            {
                whereClauses.Add("p.[Название] LIKE @SearchTerm");
            }

            var whereSql = string.Join(" AND ", whereClauses);
            var offset = (pageNumber - 1) * pageSize;

            var sql = $@"
                SELECT p.[Код], p.[Название], p.[Описание], p.[КодМенеджера], 
                       p.[ДатаНачала], p.[ДатаОкончания], p.[CreatedAt], p.[IsDeleted],
                       u.[Имя] AS [ManagerName]
                FROM [dbo].[Проект] p
                LEFT JOIN [dbo].[Пользователь] u ON p.[КодМенеджера] = u.[Код]
                WHERE {whereSql}
                ORDER BY p.[CreatedAt] DESC
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

            var results = await QueryAsync(sql, new
            {
                SearchTerm = string.IsNullOrEmpty(searchTerm) ? null : $"%{searchTerm}%",
                Offset = offset,
                PageSize = pageSize
            }, reader => new Project
            {
                Id = reader.GetInt32(0),
                Title = reader.GetString(1),
                Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                ManagerId = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                Manager = reader.IsDBNull(8) ? null : new User { Name = reader.GetString(8) },
                StartDate = reader.IsDBNull(4) ? null : reader.GetDateTime(4),
                EndDate = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                CreatedAt = reader.GetDateTime(6),
                IsDeleted = reader.GetBoolean(7)
            });

            var countSql = $@"SELECT COUNT(*) FROM [dbo].[Проект] p WHERE {whereSql}";
            var total = await ExecuteScalarAsync<int>(countSql, new
            {
                SearchTerm = string.IsNullOrEmpty(searchTerm) ? null : $"%{searchTerm}%"
            });

            return new PagedResult<Project>
            {
                Items = results,
                TotalCount = total,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        public async Task<Project?> GetByIdAsync(int id)
        {
            const string sql = @"
                SELECT p.[Код], p.[Название], p.[Описание], p.[КодМенеджера], 
                       p.[ДатаНачала], p.[ДатаОкончания], p.[CreatedAt], p.[IsDeleted],
                       u.[Имя] AS [ManagerName]
                FROM [dbo].[Проект] p
                LEFT JOIN [dbo].[Пользователь] u ON p.[КодМенеджера] = u.[Код]
                WHERE p.[Код] = @Id AND p.[IsDeleted] = 0";

            return await QueryFirstOrDefaultAsync(sql, new { Id = id }, reader => new Project
            {
                Id = reader.GetInt32(0),
                Title = reader.GetString(1),
                Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                ManagerId = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                Manager = reader.IsDBNull(8) ? null : new User { Name = reader.GetString(8) },
                StartDate = reader.IsDBNull(4) ? null : reader.GetDateTime(4),
                EndDate = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                CreatedAt = reader.GetDateTime(6),
                IsDeleted = reader.GetBoolean(7)
            });
        }

        public async Task<IEnumerable<Project>> GetAllAsync()
        {
            const string sql = @"
                SELECT [Код], [Название], [Описание], [КодМенеджера], 
                       [ДатаНачала], [ДатаОкончания], [CreatedAt], [IsDeleted]
                FROM [dbo].[Проект]
                WHERE [IsDeleted] = 0
                ORDER BY [CreatedAt] DESC";

            return await QueryAsync(sql, null, reader => new Project
            {
                Id = reader.GetInt32(0),
                Title = reader.GetString(1),
                Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                ManagerId = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                StartDate = reader.IsDBNull(4) ? null : reader.GetDateTime(4),
                EndDate = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                CreatedAt = reader.GetDateTime(6),
                IsDeleted = reader.GetBoolean(7)
            });
        }

        public async Task<IEnumerable<Project>> GetByManagerIdAsync(int managerId)
        {
            const string sql = @"
                SELECT [Код], [Название], [Описание], [КодМенеджера], 
                       [ДатаНачала], [ДатаОкончания], [CreatedAt], [IsDeleted]
                FROM [dbo].[Проект]
                WHERE [КодМенеджера] = @ManagerId AND [IsDeleted] = 0
                ORDER BY [CreatedAt] DESC";

            return await QueryAsync(sql, new { ManagerId = managerId }, reader => new Project
            {
                Id = reader.GetInt32(0),
                Title = reader.GetString(1),
                Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                ManagerId = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                StartDate = reader.IsDBNull(4) ? null : reader.GetDateTime(4),
                EndDate = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                CreatedAt = reader.GetDateTime(6),
                IsDeleted = reader.GetBoolean(7)
            });
        }

        public async Task<int> CreateAsync(Project project)
        {
            const string sql = @"
                INSERT INTO [dbo].[Проект] ([Название], [Описание], [КодМенеджера], [ДатаНачала], [ДатаОкончания])
                VALUES (@Title, @Description, @ManagerId, @StartDate, @EndDate);
                SELECT SCOPE_IDENTITY();";

            return await ExecuteScalarAsync<int>(sql, new
            {
                project.Title,
                Description = project.Description ?? (object)DBNull.Value,
                ManagerId = project.ManagerId ?? (object)DBNull.Value,
                StartDate = project.StartDate ?? (object)DBNull.Value,
                EndDate = project.EndDate ?? (object)DBNull.Value
            });
        }

        public async Task<bool> UpdateAsync(Project project)
        {
            const string sql = @"
                UPDATE [dbo].[Проект]
                SET [Название] = @Title,
                    [Описание] = @Description,
                    [КодМенеджера] = @ManagerId,
                    [ДатаНачала] = @StartDate,
                    [ДатаОкончания] = @EndDate
                WHERE [Код] = @Id AND [IsDeleted] = 0";

            var rowsAffected = await ExecuteNonQueryAsync(sql, new
            {
                project.Id,
                project.Title,
                Description = project.Description ?? (object)DBNull.Value,
                ManagerId = project.ManagerId ?? (object)DBNull.Value,
                StartDate = project.StartDate ?? (object)DBNull.Value,
                EndDate = project.EndDate ?? (object)DBNull.Value
            });

            return rowsAffected > 0;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            const string sql = @"UPDATE [dbo].[Проект] SET [IsDeleted] = 1 WHERE [Код] = @Id";
            var rowsAffected = await ExecuteNonQueryAsync(sql, new { Id = id });
            return rowsAffected > 0;
        }

        public async Task<IEnumerable<User>> GetTeamMembersAsync(int projectId)
        {
            const string sql = @"
                SELECT DISTINCT u.[Код], u.[РольПользователя], u.[Имя], u.[Email], u.[ПарольHash], u.[CreatedAt], u.[IsDeleted]
                FROM [dbo].[Пользователь] u
                INNER JOIN [dbo].[Задача] t ON u.[Код] = t.[КодИсполнителя]
                WHERE t.[КодПроекта] = @ProjectId AND t.[IsDeleted] = 0 AND u.[IsDeleted] = 0";

            return await QueryAsync(sql, new { ProjectId = projectId }, reader => new User
            {
                Id = reader.GetInt32(0),
                RoleName = reader.GetString(1),
                Name = reader.GetString(2),
                Email = reader.GetString(3),
                PasswordHash = reader.GetString(4),
                CreatedAt = reader.GetDateTime(5),
                IsDeleted = reader.GetBoolean(6)
            });
        }
    }
}
