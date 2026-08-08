namespace Dibal.Domain.Auditing;

/// <summary>
/// Marks an entity as subject to the soft-delete global query filter.
/// See CLAUDE.md invariant 2 — there is no scenario where Remove() is correct
/// for an entity implementing this.
/// </summary>
public interface ISoftDeletable
{
    public bool IsDeleted { get; set; }
}
