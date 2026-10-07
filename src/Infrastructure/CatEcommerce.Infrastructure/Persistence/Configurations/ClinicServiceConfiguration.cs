using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

public sealed class ClinicServiceConfiguration : IEntityTypeConfiguration<ClinicServiceAggregate>
{
    public void Configure(EntityTypeBuilder<ClinicServiceAggregate> builder)
    {
        builder.ToTable("clinic_service", "admin");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(30).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("ix_clinic_service_code");

        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(2000).IsRequired();
        builder.Property(x => x.Category).HasColumnName("category").HasConversion(k => k.Value, v => Enumeration.FromValue<ServiceCategory>(v)).IsRequired();
        builder.Property(x => x.DurationMinutes).HasColumnName("duration_minutes").IsRequired();
        builder.Property(x => x.RequiredRoomKind).HasColumnName("required_room_kind").HasConversion(k => k.Value, v => Enumeration.FromValue<RoomKind>(v)).IsRequired();
        builder.Property(x => x.TaxClassId).HasColumnName("tax_class_id").IsRequired();
        builder.Property(x => x.IsBookableOnline).HasColumnName("is_bookable_online").IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();

        builder.PrimitiveCollection(x => x.QualifiedRoleIds).HasColumnName("qualified_role_ids").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(x => x.Tags).IsRequired(false);
        builder.Property(x => x.Language).IsRequired(false);
        builder.Property(x => x.ServiceId).IsRequired(false);
        builder.Property<uint>("xmin").IsRowVersion();
        builder.Ignore(x => x.Events);
        builder.Ignore(x => x.Metadata);
    }
}
