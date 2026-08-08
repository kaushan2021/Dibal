using Dibal.Domain.Enums;
using Dibal.Infrastructure.Auditing;
using Dibal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Npgsql.NameTranslation;

namespace Dibal.Tests;

/// <summary>
/// Runs against a real Postgres — citext, native enums, and generated columns
/// have no in-memory-provider equivalent, and those are exactly the parts of
/// the schema worth testing. Connection string comes from the
/// DIBAL_TEST_CONNECTION env var; see README.md for local setup.
/// </summary>
public class DatabaseFixture : IDisposable
{
    private readonly NpgsqlDataSource _dataSource;

    public DatabaseFixture()
    {
        var connectionString = Environment.GetEnvironmentVariable("DIBAL_TEST_CONNECTION")
            ?? "Host=127.0.0.1;Port=55432;Username=pgtest;Password=pgtest;Database=dibal_test";

        var builder = new NpgsqlDataSourceBuilder(connectionString);
        builder.MapEnum<CustomerType>("customer_type", new NpgsqlNullNameTranslator());
        _dataSource = builder.Build();

        using var db = CreateContext();
        db.Database.Migrate();
    }

    public DibalDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<DibalDbContext>()
            .UseNpgsql(_dataSource, npgsql => npgsql.MapEnum<CustomerType>("customer_type"))
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(new AuditSaveChangesInterceptor(new NullCurrentUserAccessor()))
            .Options;

        return new DibalDbContext(options);
    }

    public void Dispose() => _dataSource.Dispose();
}

[CollectionDefinition("Database")]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture>;
