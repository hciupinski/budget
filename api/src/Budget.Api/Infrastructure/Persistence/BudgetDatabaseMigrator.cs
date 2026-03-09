using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Budget.Api.Infrastructure.Persistence;

public sealed class BudgetDatabaseMigrator(
    BudgetDbContext dbContext,
    ILogger<BudgetDatabaseMigrator> logger)
{
    private readonly IMigrationsAssembly _migrationsAssembly = dbContext.GetService<IMigrationsAssembly>();

    public async Task MigrateAsync(CancellationToken cancellationToken)
    {
        await EnsureBaselineMigrationHistoryAsync(cancellationToken);
        await dbContext.Database.MigrateAsync(cancellationToken);
    }

    private async Task EnsureBaselineMigrationHistoryAsync(CancellationToken cancellationToken)
    {
        var database = dbContext.Database;
        var connection = database.GetDbConnection();
        var closeAfter = connection.State != ConnectionState.Open;

        if (closeAfter)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            var hasBudgetTables = await ExecuteExistsAsync(
                connection,
                """
                SELECT EXISTS (
                    SELECT 1
                    FROM information_schema.tables
                    WHERE table_schema = current_schema()
                      AND table_name = 'budget_categories'
                );
                """,
                cancellationToken);

            if (!hasBudgetTables)
            {
                return;
            }

            var hasHistoryTable = await ExecuteExistsAsync(
                connection,
                """
                SELECT EXISTS (
                    SELECT 1
                    FROM information_schema.tables
                    WHERE table_schema = current_schema()
                      AND table_name = '__EFMigrationsHistory'
                );
                """,
                cancellationToken);

            var hasHistoryRows = false;
            if (hasHistoryTable)
            {
                hasHistoryRows = await ExecuteExistsAsync(
                    connection,
                    """SELECT EXISTS (SELECT 1 FROM "__EFMigrationsHistory");""",
                    cancellationToken);
            }

            if (hasHistoryRows)
            {
                return;
            }

            var baselineMigrationId = _migrationsAssembly.Migrations.Keys
                .OrderBy(x => x, StringComparer.Ordinal)
                .FirstOrDefault();
            if (string.IsNullOrWhiteSpace(baselineMigrationId))
            {
                logger.LogWarning("No EF migrations were found. Skipping baseline migration history setup.");
                return;
            }

            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            await ExecuteNonQueryAsync(
                connection,
                transaction,
                """
                CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                    "MigrationId" character varying(150) NOT NULL,
                    "ProductVersion" character varying(32) NOT NULL,
                    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
                );
                """,
                cancellationToken);

            await using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText =
                """
                INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                SELECT @migrationId, @productVersion
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM "__EFMigrationsHistory"
                    WHERE "MigrationId" = @migrationId
                );
                """;
            var efProductVersion = typeof(Migration).Assembly.GetName().Version?.ToString(3) ?? "10.0.0";
            AddParameter(insert, "@migrationId", baselineMigrationId);
            AddParameter(insert, "@productVersion", efProductVersion);
            await insert.ExecuteNonQueryAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation(
                "Initialized EF migration baseline for existing schema using migration {MigrationId}.",
                baselineMigrationId);
        }
        finally
        {
            if (closeAfter)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task<bool> ExecuteExistsAsync(
        DbConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var scalar = await command.ExecuteScalarAsync(cancellationToken);
        return scalar is true || (scalar is bool flag && flag);
    }

    private static async Task ExecuteNonQueryAsync(
        DbConnection connection,
        DbTransaction transaction,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
