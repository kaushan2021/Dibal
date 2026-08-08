using Dibal.Domain.Auditing;
using Dibal.Domain.Enums;

namespace Dibal.Domain.Entities;

public class Client : ISoftDeletable, IAuditable
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public required string BusinessName { get; set; }
    public string? ClientName { get; set; }

    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? County { get; set; }
    public string? Postcode { get; set; }

    /// <summary>
    /// Database-computed (GENERATED ALWAYS): upper, spaces stripped. Never set
    /// by application code — see docs/02-schema.sql.
    /// </summary>
    public string? PostcodeNormalised { get; private set; }

    public string? Email { get; set; }
    public string? Phone { get; set; }

    public required CustomerType CustomerType { get; set; }
    public string? Notes { get; set; }

    public bool IsDeleted { get; set; }

    // Always overwritten by AuditSaveChangesInterceptor before save — never
    // set these from a page. Not `required`; the interceptor is the contract.
    public DateTimeOffset CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTimeOffset ModifiedAt { get; set; }
    public Guid ModifiedBy { get; set; }
}
