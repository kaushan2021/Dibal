namespace Dibal.Infrastructure.Auditing;

/// <summary>
/// Resolves the acting user for audit_log.changed_by and the *_by columns.
/// Implemented in Dibal.Web against HttpContext; a no-context (background
/// service, seeder) implementation returns null.
/// </summary>
public interface ICurrentUserAccessor
{
    public Guid? UserId { get; }
}
