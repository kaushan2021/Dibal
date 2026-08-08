using Dibal.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Dibal.Web.Startup;

/// <summary>
/// Creates the three fixed roles and the root Owner account on first run.
/// Idempotent — safe to run on every startup. The root account's credentials
/// come from RootAccount:Email / RootAccount:Password (user-secrets locally,
/// Fly secrets in production — see README.md). Never hardcode them.
/// </summary>
public static class IdentitySeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
            }
        }

        var userManager = services.GetRequiredService<UserManager<AppUser>>();
        var configuration = services.GetRequiredService<IConfiguration>();

        var rootEmail = configuration["RootAccount:Email"];
        var rootPassword = configuration["RootAccount:Password"];

        if (string.IsNullOrWhiteSpace(rootEmail) || string.IsNullOrWhiteSpace(rootPassword))
        {
            // Not fatal: a fresh clone without secrets set yet can still build
            // and run everything except sign-in. README.md documents the keys.
            return;
        }

        if (await userManager.FindByEmailAsync(rootEmail) is not null)
        {
            return;
        }

        var owner = new AppUser
        {
            UserName = rootEmail,
            Email = rootEmail,
            EmailConfirmed = true,
            DisplayName = "Owner",
        };

        var result = await userManager.CreateAsync(owner, rootPassword);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(owner, Roles.Owner);
        }
    }
}
