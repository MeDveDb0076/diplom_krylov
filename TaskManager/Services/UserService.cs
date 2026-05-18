using System.Data;
using TaskManager.Data;
using TaskManager.Models;

namespace TaskManager.Services
{
    public interface IUserService
    {
        Task<User?> GetByIdAsync(int id);
        Task<User?> GetByEmailAsync(string email);
        Task<User?> ValidateUserAsync(string email, string password);
        Task<IEnumerable<User>> GetAllAsync();
        Task<IEnumerable<User>> GetByRoleAsync(string roleName);
        Task<int> CreateAsync(User user, string password);
        Task<bool> UpdateAsync(User user);
        Task<bool> DeleteAsync(int id);
    }

    public class UserService : BaseRepository, IUserService
    {
        public UserService(IDbConnectionFactory connectionFactory) : base(connectionFactory) { }

        public async Task<User?> GetByIdAsync(int id)
        {
            const string sql = @"SELECT [Код], [РольПользователя], [Имя], [Email], [ПарольHash], [CreatedAt], [IsDeleted]
                                 FROM [dbo].[Пользователь] 
                                 WHERE [Код] = @Id AND [IsDeleted] = 0";
            
            return await QueryFirstOrDefaultAsync(sql, new { Id = id }, reader => new User
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

        public async Task<User?> GetByEmailAsync(string email)
        {
            const string sql = @"SELECT [Код], [РольПользователя], [Имя], [Email], [ПарольHash], [CreatedAt], [IsDeleted]
                                 FROM [dbo].[Пользователь] 
                                 WHERE [Email] = @Email AND [IsDeleted] = 0";
            
            return await QueryFirstOrDefaultAsync(sql, new { Email = email }, reader => new User
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

        public async Task<User?> ValidateUserAsync(string email, string password)
        {
            var user = await GetByEmailAsync(email);
            if (user == null) return null;

            // Проверка пароля с BCrypt
            if (BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            {
                return user;
            }

            return null;
        }

        public async Task<IEnumerable<User>> GetAllAsync()
        {
            const string sql = @"SELECT [Код], [РольПользователя], [Имя], [Email], [ПарольHash], [CreatedAt], [IsDeleted]
                                 FROM [dbo].[Пользователь] 
                                 WHERE [IsDeleted] = 0";
            
            return await QueryAsync(sql, null, reader => new User
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

        public async Task<IEnumerable<User>> GetByRoleAsync(string roleName)
        {
            const string sql = @"SELECT [Код], [РольПользователя], [Имя], [Email], [ПарольHash], [CreatedAt], [IsDeleted]
                                 FROM [dbo].[Пользователь] 
                                 WHERE [РольПользователя] = @RoleName AND [IsDeleted] = 0";
            
            return await QueryAsync(sql, new { RoleName = roleName }, reader => new User
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

        public async Task<int> CreateAsync(User user, string password)
        {
            const string sql = @"INSERT INTO [dbo].[Пользователь] ([РольПользователя], [Имя], [Email], [ПарольHash])
                                 VALUES (@RoleName, @Name, @Email, @PasswordHash);
                                 SELECT SCOPE_IDENTITY();";

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
            
            return await ExecuteScalarAsync<int>(sql, new
            {
                user.RoleName,
                user.Name,
                user.Email,
                PasswordHash = user.PasswordHash
            });
        }

        public async Task<bool> UpdateAsync(User user)
        {
            const string sql = @"UPDATE [dbo].[Пользователь] 
                                 SET [РольПользователя] = @RoleName, [Имя] = @Name, [Email] = @Email
                                 WHERE [Код] = @Id";

            var rowsAffected = await ExecuteNonQueryAsync(sql, new
            {
                user.Id,
                user.RoleName,
                user.Name,
                user.Email
            });

            return rowsAffected > 0;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            const string sql = @"UPDATE [dbo].[Пользователь] SET [IsDeleted] = 1 WHERE [Код] = @Id";
            var rowsAffected = await ExecuteNonQueryAsync(sql, new { Id = id });
            return rowsAffected > 0;
        }
    }
}
