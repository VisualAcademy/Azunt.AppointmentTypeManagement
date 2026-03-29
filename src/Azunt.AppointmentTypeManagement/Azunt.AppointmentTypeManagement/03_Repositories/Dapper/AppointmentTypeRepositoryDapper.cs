using Dapper;
using Microsoft.Data.SqlClient;

namespace Azunt.AppointmentTypeManagement
{
    public class AppointmentTypeRepositoryDapper : IAppointmentTypeRepository
    {
        private static SqlConnection GetConnection(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException("Connection string must not be null or empty.", nameof(connectionString));
            }

            return new SqlConnection(connectionString);
        }

        public async Task AddAsync(AppointmentType appointmentType, string connectionString)
        {
            if (appointmentType == null)
            {
                throw new ArgumentNullException(nameof(appointmentType));
            }

            if (string.IsNullOrWhiteSpace(appointmentType.AppointmentTypeName))
            {
                throw new ArgumentException("AppointmentTypeName must not be null or empty.", nameof(appointmentType));
            }

            const string sql = @"
INSERT INTO [dbo].[AppointmentsTypes]
    ([AppointmentTypeName], [IsActive], [DateCreated])
VALUES
    (@AppointmentTypeName, @IsActive, @DateCreated);";

            if (appointmentType.DateCreated == default)
            {
                appointmentType.DateCreated = DateTime.Now;
            }

            await using var connection = GetConnection(connectionString);
            await connection.ExecuteAsync(sql, appointmentType);
        }

        public async Task<List<AppointmentType>> GetAllAsync(string connectionString)
        {
            const string sql = @"
SELECT
    [Id],
    [AppointmentTypeName],
    [IsActive],
    [DateCreated]
FROM [dbo].[AppointmentsTypes]
ORDER BY [Id] DESC;";

            await using var connection = GetConnection(connectionString);
            var result = await connection.QueryAsync<AppointmentType>(sql);

            return result.ToList();
        }

        public async Task<AppointmentType> GetByIdAsync(long id, string connectionString)
        {
            const string sql = @"
SELECT
    [Id],
    [AppointmentTypeName],
    [IsActive],
    [DateCreated]
FROM [dbo].[AppointmentsTypes]
WHERE [Id] = @Id;";

            await using var connection = GetConnection(connectionString);
            var appointmentType = await connection.QuerySingleOrDefaultAsync<AppointmentType>(sql, new { Id = id });

            return appointmentType ?? new AppointmentType();
        }

        public async Task UpdateAsync(AppointmentType appointmentType, string connectionString)
        {
            if (appointmentType == null)
            {
                throw new ArgumentNullException(nameof(appointmentType));
            }

            if (string.IsNullOrWhiteSpace(appointmentType.AppointmentTypeName))
            {
                throw new ArgumentException("AppointmentTypeName must not be null or empty.", nameof(appointmentType));
            }

            const string sql = @"
UPDATE [dbo].[AppointmentsTypes]
SET
    [AppointmentTypeName] = @AppointmentTypeName,
    [IsActive] = @IsActive
WHERE [Id] = @Id;";

            await using var connection = GetConnection(connectionString);
            await connection.ExecuteAsync(sql, appointmentType);
        }

        public async Task DeleteAsync(long id, string connectionString)
        {
            const string sql = @"
DELETE FROM [dbo].[AppointmentsTypes]
WHERE [Id] = @Id;";

            await using var connection = GetConnection(connectionString);
            await connection.ExecuteAsync(sql, new { Id = id });
        }
    }
}