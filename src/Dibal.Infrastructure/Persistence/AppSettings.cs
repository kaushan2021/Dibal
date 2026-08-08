namespace Dibal.Infrastructure.Persistence;

/// <summary>
/// Singleton row (id is always 1 — enforced by a check constraint in the
/// migration). SMTP fields are unused until Phase 5 but the table's full shape
/// is already specified in docs/02-schema.sql, so it is created complete now.
/// </summary>
public class AppSettings
{
    public short Id { get; set; } = 1;

    public string CompanyName { get; set; } = "";
    public string CompanyAddress { get; set; } = "";
    public string? CompanyPhone { get; set; }
    public string? CompanyEmail { get; set; }
    public string? LogoStorageKey { get; set; }
    public string DocumentPrefix { get; set; } = "DN";
    public int LowStockThreshold { get; set; } = 5;

    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 587;
    public bool SmtpUseStartTls { get; set; } = true;
    public string? SmtpUsername { get; set; }

    /// <summary>Data Protection encrypted. NEVER plaintext — CLAUDE.md.</summary>
    public string? SmtpPasswordEnc { get; set; }

    public string? SmtpFromName { get; set; }
    public string? SmtpFromAddress { get; set; }
    public string? SmtpReplyTo { get; set; }

    public DateTimeOffset ModifiedAt { get; set; }
    public Guid? ModifiedBy { get; set; }
}
