namespace Dibal.Infrastructure.Auditing;

/// <summary>
/// Append-only. Written exclusively by AuditSaveChangesInterceptor — never
/// constructed or modified by application code directly.
/// </summary>
public class AuditLogEntry
{
    public long Id { get; set; }
    public required string EntityName { get; set; }
    public required string EntityId { get; set; }
    public required string Action { get; set; }
    public string? Before { get; set; }
    public string? After { get; set; }
    public Guid? ChangedBy { get; set; }
    public DateTimeOffset ChangedAt { get; set; }
}
