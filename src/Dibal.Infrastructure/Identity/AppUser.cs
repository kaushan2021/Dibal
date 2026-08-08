using Microsoft.AspNetCore.Identity;

namespace Dibal.Infrastructure.Identity;

public class AppUser : IdentityUser<Guid>
{
    public required string DisplayName { get; set; }
}
