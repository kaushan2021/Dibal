namespace Dibal.Infrastructure.Auditing;

/// <summary>
/// Used for design-time tooling (dotnet ef) and tests, where there is no
/// HttpContext to resolve a user from.
/// </summary>
public class NullCurrentUserAccessor : ICurrentUserAccessor
{
    public Guid? UserId => null;
}
