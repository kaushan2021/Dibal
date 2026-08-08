using Dibal.Domain.Entities;
using Dibal.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Dibal.Tests;

[Collection("Database")]
public class ClientPersistenceTests(DatabaseFixture fixture)
{
    [Fact]
    public async Task Soft_deleted_client_is_excluded_by_the_global_query_filter()
    {
        await using var db = fixture.CreateContext();

        var client = new Client { BusinessName = $"Test Co {Guid.NewGuid()}", CustomerType = CustomerType.Reseller };
        db.Clients.Add(client);
        await db.SaveChangesAsync();

        client.IsDeleted = true;
        await db.SaveChangesAsync();

        var found = await db.Clients.FirstOrDefaultAsync(c => c.Id == client.Id);
        Assert.Null(found);

        var stillThere = await db.Clients.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == client.Id);
        Assert.NotNull(stillThere);
    }

    [Fact]
    public async Task Postcode_is_normalised_by_the_database_on_insert()
    {
        await using var db = fixture.CreateContext();

        var client = new Client
        {
            BusinessName = $"Test Co {Guid.NewGuid()}",
            CustomerType = CustomerType.EndUser,
            Postcode = "sw1a 1aa",
        };
        db.Clients.Add(client);
        await db.SaveChangesAsync();

        await db.Entry(client).ReloadAsync();
        Assert.Equal("SW1A1AA", client.PostcodeNormalised);
    }

    [Fact]
    public async Task AuditSaveChangesInterceptor_stamps_and_logs_an_insert()
    {
        await using var db = fixture.CreateContext();

        var client = new Client { BusinessName = $"Test Co {Guid.NewGuid()}", CustomerType = CustomerType.Reseller };
        db.Clients.Add(client);
        await db.SaveChangesAsync();

        Assert.NotEqual(default, client.CreatedAt);
        Assert.NotEqual(default, client.ModifiedAt);

        var logEntry = await db.AuditLog
            .Where(a => a.EntityName == nameof(Client) && a.EntityId == client.Id.ToString())
            .OrderByDescending(a => a.ChangedAt)
            .FirstOrDefaultAsync();

        Assert.NotNull(logEntry);
        Assert.Equal("Added", logEntry.Action);
        Assert.Contains(client.BusinessName, logEntry.After);
    }
}
