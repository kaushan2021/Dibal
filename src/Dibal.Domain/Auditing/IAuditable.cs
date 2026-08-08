namespace Dibal.Domain.Auditing;

/// <summary>
/// Entities carrying created/modified stamps. Populated by
/// AuditSaveChangesInterceptor, never set directly by application code.
/// </summary>
public interface IAuditable
{
    public DateTimeOffset CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTimeOffset ModifiedAt { get; set; }
    public Guid ModifiedBy { get; set; }
}
