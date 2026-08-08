using Dibal.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dibal.Infrastructure.Persistence.Configurations;

public class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.BusinessName).IsRequired();
        builder.Property(c => c.Email).HasColumnType("citext");

        // Generated column — computed by Postgres, never written by EF.
        // Column name in the SQL fragment must match the snake_case naming
        // convention's output for Postcode ("postcode").
        builder.Property(c => c.PostcodeNormalised)
            .HasComputedColumnSql("upper(replace(coalesce(postcode, ''), ' ', ''))", stored: true);

        // The (expression, modelName) overload is required so the two
        // HasIndex(c => c.BusinessName) calls don't get matched to the same
        // index object — a bare HasIndex(expr) is matched by property list,
        // and a second call would just overwrite the first's config instead
        // of creating a second index. HasDatabaseName still has to be set
        // explicitly on each — the modelName argument does not become the
        // SQL name by itself.
        builder.HasIndex(c => c.BusinessName, "ix_clients_business_trgm")
            .HasDatabaseName("ix_clients_business_trgm")
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops");

        builder.HasIndex(c => c.ClientName, "ix_clients_client_trgm")
            .HasDatabaseName("ix_clients_client_trgm")
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops");

        builder.HasIndex(c => c.PostcodeNormalised, "ix_clients_postcode")
            .HasDatabaseName("ix_clients_postcode");

        builder.HasIndex(c => c.BusinessName, "ix_clients_active")
            .HasDatabaseName("ix_clients_active")
            .HasFilter("NOT is_deleted");
    }
}
