using Dibal.Domain.Auditing;
using Dibal.Domain.Entities;
using Dibal.Domain.Enums;
using Dibal.Infrastructure.Auditing;
using Dibal.Infrastructure.Identity;
using Dibal.Infrastructure.Persistence.Configurations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Npgsql.NameTranslation;

namespace Dibal.Infrastructure.Persistence;

public class DibalDbContext(DbContextOptions<DibalDbContext> options)
    : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<AppSettings> AppSettings => Set<AppSettings>();
    public DbSet<AuditLogEntry> AuditLog => Set<AuditLogEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasPostgresExtension("pg_trgm");
        modelBuilder.HasPostgresExtension("citext");

        // Null translator: labels come out as the raw CLR member names
        // ("Reseller", "EndUser"), matching docs/02-schema.sql exactly. The
        // default snake_case translator would store "reseller"/"end_user".
        modelBuilder.HasPostgresEnum<CustomerType>(null, "customer_type", new NpgsqlNullNameTranslator());

        modelBuilder.ApplyConfiguration(new ClientConfiguration());
        modelBuilder.ApplyConfiguration(new AppSettingsConfiguration());
        modelBuilder.ApplyConfiguration(new AuditLogEntryConfiguration());

        ApplySoftDeleteFilters(modelBuilder);
    }

    /// <summary>
    /// CLAUDE.md invariant 2: soft delete via global query filter, applied to
    /// every ISoftDeletable entity without needing a per-entity HasQueryFilter
    /// call as new entities are added in later phases.
    /// </summary>
    private static void ApplySoftDeleteFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            var parameter = System.Linq.Expressions.Expression.Parameter(entityType.ClrType, "e");
            var property = System.Linq.Expressions.Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted));
            var notDeleted = System.Linq.Expressions.Expression.Not(property);
            var lambda = System.Linq.Expressions.Expression.Lambda(notDeleted, parameter);

            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
        }
    }
}
