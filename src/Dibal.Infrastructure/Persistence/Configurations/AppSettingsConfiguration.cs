using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dibal.Infrastructure.Persistence.Configurations;

public class AppSettingsConfiguration : IEntityTypeConfiguration<AppSettings>
{
    public void Configure(EntityTypeBuilder<AppSettings> builder)
    {
        builder.HasKey(s => s.Id);
        builder.ToTable(t => t.HasCheckConstraint("ck_app_settings_singleton", "id = 1"));

        // The singleton row. Seeded here (migration HasData) rather than left
        // for the app to create lazily — stock_levels reads this threshold and
        // must never see a missing row. ModifiedAt is a placeholder (epoch),
        // not "now" — migrations must be deterministic, and the real value is
        // set the first time someone saves the settings screen.
        builder.HasData(new AppSettings { Id = 1, ModifiedAt = DateTimeOffset.UnixEpoch });
    }
}
