using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace Azunt.AppointmentTypeManagement
{
    /// <summary>
    /// 각 테넌트 DB 및 마스터 DB에 AppointmentsTypes 테이블을
    /// "없으면 생성, 있으면 누락 컬럼만 추가" 방식으로 안전하게 보정합니다.
    /// 재실행해도 안전한(idempotent) 구조입니다.
    /// </summary>
    public class AppointmentsTypesTableBuilder
    {
        private readonly string _connectionString;
        private readonly ILogger<AppointmentsTypesTableBuilder> _logger;

        public AppointmentsTypesTableBuilder(
            string connectionString,
            ILogger<AppointmentsTypesTableBuilder> logger)
        {
            _connectionString = connectionString;
            _logger = logger;
        }

        public void BuildMasterDatabase()
        {
            try
            {
                EnsureAppointmentsTypesTable(_connectionString);
                _logger.LogInformation("AppointmentsTypes table processed (master DB).");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing AppointmentsTypes table (master DB).");
            }
        }

        public void BuildTenantDatabases()
        {
            var tenantConnectionStrings = GetTenantConnectionStrings();

            for (int i = 0; i < tenantConnectionStrings.Count; i++)
            {
                var connStr = tenantConnectionStrings[i];
                var tenantIndex = i + 1;

                try
                {
                    EnsureAppointmentsTypesTable(connStr);
                    _logger.LogInformation("AppointmentsTypes table processed (tenant DB #{Index}).", tenantIndex);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing tenant DB #{Index}.", tenantIndex);
                }
            }
        }

        private List<string> GetTenantConnectionStrings()
        {
            var result = new List<string>();

            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            using var cmd = new SqlCommand("SELECT ConnectionString FROM dbo.Tenants", connection);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                var connStr = reader["ConnectionString"]?.ToString();
                if (!string.IsNullOrWhiteSpace(connStr))
                {
                    result.Add(connStr);
                }
            }

            return result;
        }

        private void EnsureAppointmentsTypesTable(string connectionString)
        {
            using var connection = new SqlConnection(connectionString);
            connection.Open();

            using var checkTableCmd = new SqlCommand(@"
SELECT COUNT(*)
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_SCHEMA = 'dbo'
  AND TABLE_NAME = 'AppointmentsTypes';", connection);

            int exists = (int)checkTableCmd.ExecuteScalar();

            if (exists == 0)
            {
                using var createCmd = new SqlCommand(@"
-- 예약 종류 관리 테이블
-- NOTE:
-- 'AppointmentsTypes' 테이블명은 문법적으로는 다소 어색할 수 있으나,
-- SQL Server의 테이블 목록에서 'Appointments' 테이블 바로 아래에 정렬되어
-- 관련 테이블임을 쉽게 식별할 수 있도록 의도적으로 사용한 이름입니다.
CREATE TABLE [dbo].[AppointmentsTypes]
(
    [Id] BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [AppointmentTypeName] NVARCHAR(50) NOT NULL,
    [IsActive] BIT NOT NULL CONSTRAINT [DF_AppointmentsTypes_IsActive] DEFAULT ((1)),
    [DateCreated] DATETIME NOT NULL CONSTRAINT [DF_AppointmentsTypes_DateCreated] DEFAULT (GETDATE()),
    [TenantId] BIGINT NULL
);", connection);

                createCmd.ExecuteNonQuery();
                _logger.LogInformation("AppointmentsTypes table created.");
            }
            else
            {
                var expectedColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["AppointmentTypeName"] = "NVARCHAR(50) NULL",
                    ["IsActive"] = "BIT NULL",
                    ["DateCreated"] = "DATETIME NULL",
                    ["TenantId"] = "BIGINT NULL"
                };

                foreach (var column in expectedColumns)
                {
                    using var checkColumnCmd = new SqlCommand(@"
SELECT COUNT(*)
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'dbo'
  AND TABLE_NAME = 'AppointmentsTypes'
  AND COLUMN_NAME = @ColumnName;", connection);

                    checkColumnCmd.Parameters.AddWithValue("@ColumnName", column.Key);

                    int columnExists = (int)checkColumnCmd.ExecuteScalar();

                    if (columnExists == 0)
                    {
                        using var alterCmd = new SqlCommand($@"
ALTER TABLE [dbo].[AppointmentsTypes]
ADD [{column.Key}] {column.Value};", connection);

                        alterCmd.ExecuteNonQuery();
                        _logger.LogInformation("Column [{Column}] added to AppointmentsTypes table.", column.Key);
                    }
                }

                EnsurePrimaryKeyOnId(connection);
                EnsureIsActiveDefault(connection);
                EnsureDateCreatedDefault(connection);
            }
        }

        private void EnsurePrimaryKeyOnId(SqlConnection connection)
        {
            using var cmd = new SqlCommand(@"
IF EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'dbo'
      AND TABLE_NAME = 'AppointmentsTypes'
      AND COLUMN_NAME = 'Id'
)
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM sys.key_constraints kc
        WHERE kc.parent_object_id = OBJECT_ID(N'[dbo].[AppointmentsTypes]')
          AND kc.type = 'PK'
    )
    BEGIN
        ALTER TABLE [dbo].[AppointmentsTypes]
        ADD PRIMARY KEY CLUSTERED ([Id] ASC);
    END
END", connection);

            cmd.ExecuteNonQuery();
        }

        private void EnsureIsActiveDefault(SqlConnection connection)
        {
            using var cmd = new SqlCommand(@"
IF EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'dbo'
      AND TABLE_NAME = 'AppointmentsTypes'
      AND COLUMN_NAME = 'IsActive'
)
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM sys.default_constraints dc
        INNER JOIN sys.columns c
            ON dc.parent_object_id = c.object_id
           AND dc.parent_column_id = c.column_id
        WHERE dc.parent_object_id = OBJECT_ID(N'[dbo].[AppointmentsTypes]')
          AND c.name = N'IsActive'
    )
    BEGIN
        ALTER TABLE [dbo].[AppointmentsTypes]
        ADD CONSTRAINT [DF_AppointmentsTypes_IsActive] DEFAULT ((1)) FOR [IsActive];
    END
END", connection);

            cmd.ExecuteNonQuery();
        }

        private void EnsureDateCreatedDefault(SqlConnection connection)
        {
            using var cmd = new SqlCommand(@"
IF EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'dbo'
      AND TABLE_NAME = 'AppointmentsTypes'
      AND COLUMN_NAME = 'DateCreated'
)
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM sys.default_constraints dc
        INNER JOIN sys.columns c
            ON dc.parent_object_id = c.object_id
           AND dc.parent_column_id = c.column_id
        WHERE dc.parent_object_id = OBJECT_ID(N'[dbo].[AppointmentsTypes]')
          AND c.name = N'DateCreated'
    )
    BEGIN
        ALTER TABLE [dbo].[AppointmentsTypes]
        ADD CONSTRAINT [DF_AppointmentsTypes_DateCreated] DEFAULT (GETDATE()) FOR [DateCreated];
    END
END", connection);

            cmd.ExecuteNonQuery();
        }

        public static void Run(IServiceProvider services, bool forMaster, string? optionalConnectionString = null)
        {
            try
            {
                var logger = services.GetRequiredService<ILogger<AppointmentsTypesTableBuilder>>();
                var config = services.GetRequiredService<IConfiguration>();

                string connectionString;
                if (!string.IsNullOrWhiteSpace(optionalConnectionString))
                {
                    connectionString = optionalConnectionString!;
                }
                else
                {
                    var tempConnectionString = config.GetConnectionString("DefaultConnection");
                    if (string.IsNullOrWhiteSpace(tempConnectionString))
                    {
                        throw new InvalidOperationException("DefaultConnection is not configured in appsettings.json.");
                    }

                    connectionString = tempConnectionString;
                }

                var builder = new AppointmentsTypesTableBuilder(connectionString, logger);

                if (forMaster)
                {
                    builder.BuildMasterDatabase();
                }
                else
                {
                    builder.BuildTenantDatabases();
                }
            }
            catch (Exception ex)
            {
                var fallbackLogger = services.GetService<ILogger<AppointmentsTypesTableBuilder>>();
                fallbackLogger?.LogError(ex, "Error running AppointmentsTypesTableBuilder.Run");
            }
        }
    }
}