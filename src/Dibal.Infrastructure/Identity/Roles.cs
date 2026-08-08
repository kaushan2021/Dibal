namespace Dibal.Infrastructure.Identity;

/// <summary>
/// The three roles from docs/00-overview.md. Fixed set — not user-configurable.
/// </summary>
public static class Roles
{
    public const string Owner = "Owner";
    public const string Manager = "Manager";
    public const string Staff = "Staff";

    public static readonly IReadOnlyList<string> All = [Owner, Manager, Staff];

    /// <summary>Roles allowed to create/edit/soft-delete per CLAUDE.md-derived rules.</summary>
    public const string ManagerAndAbove = $"{Owner},{Manager}";
}
