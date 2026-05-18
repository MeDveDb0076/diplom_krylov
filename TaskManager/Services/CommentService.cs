using System.Data;
using TaskManager.Data;
using TaskManager.Models;

namespace TaskManager.Services
{
    public interface ICommentService
    {
        Task<IEnumerable<Comment>> GetByTaskIdAsync(int taskId);
        Task<Comment?> GetByIdAsync(int id);
        Task<int> CreateAsync(Comment comment);
        Task<bool> DeleteAsync(int id, int currentUserId, bool isAdmin);
        Task<bool> IsAuthorAsync(int commentId, int userId);
    }

    public class CommentService : BaseRepository, ICommentService
    {
        public CommentService(IDbConnectionFactory connectionFactory) : base(connectionFactory) { }

        public async Task<IEnumerable<Comment>> GetByTaskIdAsync(int taskId)
        {
            const string sql = @"
                SELECT c.[Код], c.[КодЗадачи], c.[КодПользователя], c.[Текст], c.[ДатаСоздания], c.[IsDeleted],
                       u.[Имя] AS [AuthorName]
                FROM [dbo].[Комментарий] c
                LEFT JOIN [dbo].[Пользователь] u ON c.[КодПользователя] = u.[Код]
                WHERE c.[КодЗадачи] = @TaskId AND c.[IsDeleted] = 0
                ORDER BY c.[ДатаСоздания] ASC";

            return await QueryAsync(sql, new { TaskId = taskId }, reader => new Comment
            {
                Id = reader.GetInt32(0),
                TaskId = reader.GetInt32(1),
                UserId = reader.GetInt32(2),
                Text = reader.GetString(3),
                CreatedAt = reader.GetDateTime(4),
                IsDeleted = reader.GetBoolean(5),
                Author = new User
                {
                    Name = reader.IsDBNull(6) ? string.Empty : reader.GetString(6)
                }
            });
        }

        public async Task<Comment?> GetByIdAsync(int id)
        {
            const string sql = @"
                SELECT c.[Код], c.[КодЗадачи], c.[КодПользователя], c.[Текст], c.[ДатаСоздания], c.[IsDeleted],
                       u.[Имя] AS [AuthorName]
                FROM [dbo].[Комментарий] c
                LEFT JOIN [dbo].[Пользователь] u ON c.[КодПользователя] = u.[Код]
                WHERE c.[Код] = @Id AND c.[IsDeleted] = 0";

            return await QueryFirstOrDefaultAsync(sql, new { Id = id }, reader => new Comment
            {
                Id = reader.GetInt32(0),
                TaskId = reader.GetInt32(1),
                UserId = reader.GetInt32(2),
                Text = reader.GetString(3),
                CreatedAt = reader.GetDateTime(4),
                IsDeleted = reader.GetBoolean(5),
                Author = new User
                {
                    Name = reader.IsDBNull(6) ? string.Empty : reader.GetString(6)
                }
            });
        }

        public async Task<int> CreateAsync(Comment comment)
        {
            const string sql = @"
                INSERT INTO [dbo].[Комментарий] ([КодЗадачи], [КодПользователя], [Текст])
                VALUES (@TaskId, @UserId, @Text);
                SELECT SCOPE_IDENTITY();";

            return await ExecuteScalarAsync<int>(sql, new
            {
                comment.TaskId,
                comment.UserId,
                comment.Text
            });
        }

        public async Task<bool> DeleteAsync(int id, int currentUserId, bool isAdmin)
        {
            // Проверяем, является ли пользователь автором или администратором
            if (!isAdmin)
            {
                var isAuthor = await IsAuthorAsync(id, currentUserId);
                if (!isAuthor)
                {
                    return false; // Только автор или админ может удалять
                }
            }

            const string sql = @"UPDATE [dbo].[Комментарий] SET [IsDeleted] = 1 WHERE [Код] = @Id";
            var rowsAffected = await ExecuteNonQueryAsync(sql, new { Id = id });
            return rowsAffected > 0;
        }

        public async Task<bool> IsAuthorAsync(int commentId, int userId)
        {
            const string sql = @"SELECT COUNT(*) FROM [dbo].[Комментарий] WHERE [Код] = @Id AND [КодПользователя] = @UserId";
            var count = await ExecuteScalarAsync<int>(sql, new { Id = commentId, UserId = userId });
            return count > 0;
        }
    }
}
