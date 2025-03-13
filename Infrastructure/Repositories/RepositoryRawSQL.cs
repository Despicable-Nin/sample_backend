// Note: This implementation requires the Microsoft.Data.SqlClient NuGet package.
// Ensure you install it in the Infrastructure project via the NuGet Package Manager or:
// Install-Package Microsoft.Data.SqlClient

using Application.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Infrastructure.Repositories
{
    public class RepositoryRawSQL<T> : IRepository<T> where T : class
    {
        private readonly string _connectionString;

        // Accept IConfiguration to automatically retrieve the connection string from appsettings.json
        public RepositoryRawSQL(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        private SqlConnection GetConnection()
        {
            return new SqlConnection(_connectionString);
        }

        // Helper: Convert a DataReader row to a Dictionary<string, object?>
        private Dictionary<string, object?> ReadRow(SqlDataReader reader)
        {
            var dict = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                dict[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }
            return dict;
        }

        public async Task<IEnumerable<T>> GetAllAsync()
        {
            var result = new List<T>();
            using var connection = GetConnection();
            await connection.OpenAsync();
            var tableName = typeof(T).Name + "s"; // basic pluralization
            var command = new SqlCommand($"SELECT * FROM {tableName}", connection);
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var rowDict = ReadRow(reader);
                var json = JsonSerializer.Serialize(rowDict);
                var entity = JsonSerializer.Deserialize<T>(json);
                if (entity != null)
                    result.Add(entity);
            }
            return result;
        }

        public async Task<T?> GetByIdAsync(int id)
        {
            using var connection = GetConnection();
            await connection.OpenAsync();
            var tableName = typeof(T).Name + "s";
            var command = new SqlCommand($"SELECT * FROM {tableName} WHERE Id = @id", connection);
            command.Parameters.AddWithValue("@id", id);
            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var rowDict = ReadRow(reader);
                var json = JsonSerializer.Serialize(rowDict);
                return JsonSerializer.Deserialize<T>(json);
            }
            return null;
        }

        public async Task AddAsync(T entity)
        {
            using var connection = GetConnection();
            await connection.OpenAsync();
            var tableName = typeof(T).Name + "s";

            var props = typeof(T).GetProperties().Where(p => p.Name != "Id").ToList();
            var columns = string.Join(", ", props.Select(p => p.Name));
            var parameters = string.Join(", ", props.Select(p => "@" + p.Name));

            var command = new SqlCommand($"INSERT INTO {tableName} ({columns}) VALUES ({parameters})", connection);
            foreach (var prop in props)
            {
                command.Parameters.AddWithValue("@" + prop.Name, prop.GetValue(entity) ?? DBNull.Value);
            }
            await command.ExecuteNonQueryAsync();
        }

        public async Task UpdateAsync(T entity)
        {
            using var connection = GetConnection();
            await connection.OpenAsync();
            var tableName = typeof(T).Name + "s";
            var idProp = typeof(T).GetProperty("Id");
            if (idProp == null)
                throw new Exception("Entity must have an Id property.");

            var idValue = idProp.GetValue(entity);
            if (idValue == null)
                throw new Exception("Id value cannot be null.");

            var props = typeof(T).GetProperties().Where(p => p.Name != "Id").ToList();
            var setClause = string.Join(", ", props.Select(p => $"{p.Name} = @{p.Name}"));

            var command = new SqlCommand($"UPDATE {tableName} SET {setClause} WHERE Id = @Id", connection);
            command.Parameters.AddWithValue("@Id", idValue);
            foreach (var prop in props)
            {
                command.Parameters.AddWithValue("@" + prop.Name, prop.GetValue(entity) ?? DBNull.Value);
            }
            await command.ExecuteNonQueryAsync();
        }

        public async Task DeleteAsync(int id)
        {
            using var connection = GetConnection();
            await connection.OpenAsync();
            var tableName = typeof(T).Name + "s";
            var command = new SqlCommand($"DELETE FROM {tableName} WHERE Id = @id", connection);
            command.Parameters.AddWithValue("@id", id);
            await command.ExecuteNonQueryAsync();
        }
    }
}
