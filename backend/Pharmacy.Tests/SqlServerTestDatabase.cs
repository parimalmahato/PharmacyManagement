using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Infrastructure.Persistence;

namespace Pharmacy.IntegrationTests;

/// <summary>Creates a disposable, uniquely named SQL Server database for each test.</summary>
public sealed class SqlServerTestDatabase : IAsyncDisposable
{
    private readonly string _databaseName;
    private readonly string _masterConnectionString;
    private readonly DbContextOptions<PharmacyDbContext> _options;

    private SqlServerTestDatabase(string databaseName, string masterConnectionString,
        DbContextOptions<PharmacyDbContext> options)
    {
        _databaseName = databaseName;
        _masterConnectionString = masterConnectionString;
        _options = options;
    }

    public PharmacyDbContext NewContext() => new(_options);
    public PharmacyDbContext NewContext(Pharmacy.Application.Branches.IBranchContext branch) => new(_options, branch);

    public static async Task<SqlServerTestDatabase> CreateAsync()
    {
        var baseConnection = Environment.GetEnvironmentVariable("PHARMACY_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(baseConnection))
            throw new InvalidOperationException(
                "Set PHARMACY_TEST_SQLSERVER to a NON-PRODUCTION SQL Server connection string. " +
                "The login must have CREATE DATABASE and DROP DATABASE permissions.");

        var master = new SqlConnectionStringBuilder(baseConnection) { InitialCatalog = "master" };
        var dbName = "Pharmacy_IT_" + Guid.NewGuid().ToString("N");
        var testConnection = new SqlConnectionStringBuilder(baseConnection) { InitialCatalog = dbName };
        var options = new DbContextOptionsBuilder<PharmacyDbContext>()
            .UseSqlServer(testConnection.ConnectionString)
            .Options;

        await using (var connection = new SqlConnection(master.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE [{dbName}]";
            await command.ExecuteNonQueryAsync();
        }

        var database = new SqlServerTestDatabase(dbName, master.ConnectionString, options);
        try
        {
            await using var context = database.NewContext();
            await context.Database.MigrateAsync();
            return database;
        }
        catch
        {
            await database.DisposeAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await using var connection = new SqlConnection(_masterConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            IF DB_ID(N'{_databaseName}') IS NOT NULL
            BEGIN
                ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE [{_databaseName}];
            END
            """;
        await command.ExecuteNonQueryAsync();
    }
}
