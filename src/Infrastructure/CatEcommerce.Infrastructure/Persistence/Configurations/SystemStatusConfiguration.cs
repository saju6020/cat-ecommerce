using CatEcommerce.Domain.Aggregates.Administration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

public sealed class SystemStatusConfiguration : IEntityTypeConfiguration<SystemStatusAggregate>
{
    public void Configure(EntityTypeBuilder<SystemStatusAggregate> builder)
    {
        builder.ToTable("system_status", "admin");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.MaintenanceMode).HasColumnName("maintenance_mode").IsRequired();
        builder.Property(x => x.MaintenanceMessage).HasColumnName("maintenance_message").HasMaxLength(500);
        builder.Property(x => x.ChangedOn).HasColumnName("changed_on").HasColumnType("timestamptz").IsRequired();

        builder.Property(x => x.Tags).IsRequired(false);
        builder.Property(x => x.Language).IsRequired(false);
        builder.Property(x => x.ServiceId).IsRequired(false);
        builder.Property<uint>("xmin").IsRowVersion();
        builder.Ignore(x => x.Events);
        builder.Ignore(x => x.Metadata);
    }
}
