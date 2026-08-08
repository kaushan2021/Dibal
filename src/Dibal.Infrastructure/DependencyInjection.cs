using Dibal.Domain.Enums;
using Dibal.Infrastructure.Auditing;
using Dibal.Infrastructure.Identity;
using Dibal.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Npgsql.NameTranslation;

namespace Dibal.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddDibalInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Missing ConnectionStrings:Default. Set it via user-secrets (see README.md).");

        // The same enum labels as DibalDbContext.OnModelCreating's HasPostgresEnum
        // call — this mapping is for the ADO layer (query parameters/results),
        // that one is for migration SQL generation. Both must agree.
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.MapEnum<CustomerType>("customer_type", new NpgsqlNullNameTranslator());
        var dataSource = dataSourceBuilder.Build();

        services.AddSingleton(dataSource);

        services.AddDbContext<DibalDbContext>((sp, options) =>
        {
            options.UseNpgsql(sp.GetRequiredService<NpgsqlDataSource>(), npgsql => npgsql.MapEnum<CustomerType>("customer_type"))
                .UseSnakeCaseNamingConvention()
                .AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>());
        });

        services.AddScoped<AuditSaveChangesInterceptor>();

        services.AddIdentityCore<AppUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedAccount = false;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<DibalDbContext>()
            .AddSignInManager();

        services.AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddIdentityCookies();

        return services;
    }
}
