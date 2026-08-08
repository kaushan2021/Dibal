using System.Text.Json;
using Dibal.Domain.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Dibal.Infrastructure.Auditing;

/// <summary>
/// Stamps IAuditable entities and writes one audit_log row per changed entity,
/// in the same SaveChanges call — and therefore the same transaction — as the
/// business change. See CLAUDE.md: "Written by an EF Core SaveChanges
/// interceptor." Never construct AuditLogEntry rows any other way.
/// </summary>
public class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = false };
    private readonly ICurrentUserAccessor _currentUser;

    public AuditSaveChangesInterceptor(ICurrentUserAccessor currentUser)
    {
        _currentUser = currentUser;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
        {
            Process(eventData.Context);
        }

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            Process(eventData.Context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Process(DbContext context)
    {
        var now = DateTimeOffset.UtcNow;
        var userId = _currentUser.UserId ?? Guid.Empty;

        var entries = context.ChangeTracker.Entries()
            .Where(e => e.Entity is not AuditLogEntry
                        && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        foreach (var entry in entries)
        {
            StampAuditable(entry, now, userId);
            context.Set<AuditLogEntry>().Add(BuildLogEntry(entry, now, userId));
        }
    }

    private static void StampAuditable(EntityEntry entry, DateTimeOffset now, Guid userId)
    {
        if (entry.Entity is not IAuditable auditable)
        {
            return;
        }

        if (entry.State == EntityState.Added)
        {
            auditable.CreatedAt = now;
            auditable.CreatedBy = userId;
        }

        auditable.ModifiedAt = now;
        auditable.ModifiedBy = userId;
    }

    private static AuditLogEntry BuildLogEntry(EntityEntry entry, DateTimeOffset now, Guid userId)
    {
        var entityName = entry.Entity.GetType().Name;
        var entityId = entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey())?.CurrentValue?.ToString() ?? "";

        string? before = entry.State is EntityState.Modified or EntityState.Deleted
            ? Serialize(entry.Properties.ToDictionary(p => p.Metadata.Name, p => p.OriginalValue))
            : null;

        string? after = entry.State is EntityState.Added or EntityState.Modified
            ? Serialize(entry.Properties.ToDictionary(p => p.Metadata.Name, p => p.CurrentValue))
            : null;

        return new AuditLogEntry
        {
            EntityName = entityName,
            EntityId = entityId,
            Action = entry.State.ToString(),
            Before = before,
            After = after,
            ChangedBy = userId,
            ChangedAt = now,
        };
    }

    private static string Serialize(Dictionary<string, object?> values) =>
        JsonSerializer.Serialize(values, _jsonOptions);
}
