using System.Data;
using System.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace TaskManager.Data
{
    /// <summary>
    /// Фабрика подключений к базе данных (Singleton)
    /// </summary>
    public interface IDbConnectionFactory
    {
        IDbConnection CreateConnection();
    }

    public class DbConnectionFactory : IDbConnectionFactory
    {
        private readonly string _connectionString;

        public DbConnectionFactory(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") 
                ?? throw new ArgumentNullException(nameof(configuration), "Connection string 'DefaultConnection' not found");
        }

        public IDbConnection CreateConnection()
        {
            return new SqlConnection(_connectionString);
        }
    }

    /// <summary>
    /// Базовый репозиторий для работы с БД через ADO.NET
    /// </summary>
    public abstract class BaseRepository
    {
        protected readonly IDbConnectionFactory _connectionFactory;

        protected BaseRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        protected async Task<T?> ExecuteScalarAsync<T>(string sql, object? param = null)
        {
            using var connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandType = CommandType.Text;

            if (param != null)
            {
                AddParameters(command, param);
            }

            var result = await command.ExecuteScalarAsync();
            return result == null || result == DBNull.Value ? default : (T)Convert.ChangeType(result, typeof(T));
        }

        protected async Task<int> ExecuteNonQueryAsync(string sql, object? param = null)
        {
            using var connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandType = CommandType.Text;

            if (param != null)
            {
                AddParameters(command, param);
            }

            return await command.ExecuteNonQueryAsync();
        }

        protected async Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null, Func<IDataReader, T> mapper = null!)
        {
            var results = new List<T>();
            using var connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandType = CommandType.Text;

            if (param != null)
            {
                AddParameters(command, param);
            }

            using var reader = await command.ExecuteReaderAsync();
            
            if (mapper != null)
            {
                while (await reader.ReadAsync())
                {
                    results.Add(mapper(reader));
                }
            }

            return results;
        }

        protected async Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? param = null, Func<IDataReader, T>? mapper = null)
        {
            using var connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandType = CommandType.Text;

            if (param != null)
            {
                AddParameters(command, param);
            }

            using var reader = await command.ExecuteReaderAsync();
            
            if (mapper != null && await reader.ReadAsync())
            {
                return mapper(reader);
            }

            return default;
        }

        private void AddParameters(IDbCommand command, object param)
        {
            if (param == null) return;

            var properties = param.GetType().GetProperties();
            foreach (var prop in properties)
            {
                var value = prop.GetValue(param);
                var parameter = command.CreateParameter();
                parameter.ParameterName = $"@{prop.Name}";
                parameter.Value = value ?? DBNull.Value;
                
                // Обработка nullable типов
                if (value == null)
                {
                    parameter.DbType = DbType.String;
                }

                command.Parameters.Add(parameter);
            }
        }
    }
}
