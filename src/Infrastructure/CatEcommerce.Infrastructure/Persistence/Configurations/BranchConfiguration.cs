using CatEcommerce.Domain.Aggregates.Administration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>HO-ADM-001 §12.1.</summary>
public sealed class BranchConfiguration : IEntityTypeConfiguration<BranchAggregate>
{
    public void Configure(EntityTypeBuilder<BranchAggregate> builder)
    {
        builder.ToTable("branch", "admin");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(20).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("ix_branch_code");

        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.TimeZoneId).HasColumnName("time_zone_id").HasMaxLength(100).IsRequired();
        builder.Property(x => x.AddressLine).HasColumnName("address_line").HasMaxLength(500);
        builder.Property(x => x.PhoneNumber).HasColumnName("phone_number").HasMaxLength(30);
        builder.Property(x => x.Email).HasColumnName("email").HasMaxLength(256);
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();

        builder.OwnsMany(x => x.WeeklyHours, w =>
        {
            w.ToTable("branch_opening_interval", "admin");
            w.WithOwner().HasForeignKey("branch_id");
            w.HasKey("Id");
            w.Property(p => p.Id).ValueGeneratedNever().HasColumnName("id");
            w.Property(p => p.DayOfWeek).HasColumnName("day_of_week").IsRequired();
            w.Property(p => p.Opens).HasColumnName("opens").HasColumnType("time").IsRequired();
            w.Property(p => p.Closes).HasColumnName("closes").HasColumnType("time").IsRequired();
        });
        builder.Metadata.FindNavigation(nameof(BranchAggregate.WeeklyHours))!.SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(x => x.Tags).IsRequired(false);
        builder.Property(x => x.Language).IsRequired(false);
        builder.Property(x => x.ServiceId).IsRequired(false);
        builder.Property<uint>("xmin").IsRowVersion();
        builder.Ignore(x => x.Events);
        builder.Ignore(x => x.Metadata);
    }
}
