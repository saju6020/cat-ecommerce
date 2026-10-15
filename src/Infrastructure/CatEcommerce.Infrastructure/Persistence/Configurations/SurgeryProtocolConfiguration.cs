using CatEcommerce.Domain.Aggregates.Surgery;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>
/// HO-SRG-001 §8.12 / §12.1 (ADR-167) -- <c>clinic.surgery_protocol</c>: one row per surgical service (<c>ix_surgery_protocol_service_id</c> UNIQUE) and its
/// owned follow-up rules. Practice configuration (Operational): the owned list is replaced wholesale on an edit. No foreign key to the service (ADR-011).
/// </summary>
public sealed class SurgeryProtocolConfiguration : IEntityTypeConfiguration<SurgeryProtocolAggregate>
{
    public void Configure(EntityTypeBuilder<SurgeryProtocolAggregate> builder)
    {
        builder.ToTable("surgery_protocol", "clinic");
        builder.ConfigureAggregateBase();

        builder.Property(x => x.ProtocolServiceId).HasColumnName("service_id").IsRequired();
        builder.Property(x => x.FastingInstructions).HasColumnName("fasting_instructions").HasMaxLength(SurgeryRules.FastingInstructionsMax).IsRequired();
        builder.Property(x => x.FastingHours).HasColumnName("fasting_hours").IsRequired();
        builder.Property(x => x.PostOpInstructions).HasColumnName("post_op_instructions").HasMaxLength(SurgeryRules.PostOpInstructionsMax).IsRequired();
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by").IsRequired();
        builder.Property(x => x.UpdatedOn).HasColumnName("updated_on").HasColumnType("timestamptz").IsRequired();

        builder.OwnsMany(x => x.FollowUps, f =>
        {
            f.ToTable("surgery_protocol_follow_up", "clinic");
            f.WithOwner().HasForeignKey("protocol_id");
            f.HasKey("Id");
            f.Property(p => p.Id).ValueGeneratedNever().HasColumnName("id");
            f.Property(p => p.Kind).HasColumnName("kind").HasEnumerationValue().IsRequired();
            f.Property(p => p.DayOffset).HasColumnName("day_offset").IsRequired();
            f.Property(p => p.WindowFromDay).HasColumnName("window_from_day").IsRequired();
            f.Property(p => p.WindowToDay).HasColumnName("window_to_day").IsRequired();
        });
        builder.Metadata.FindNavigation(nameof(SurgeryProtocolAggregate.FollowUps))!.SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(x => x.ProtocolServiceId).IsUnique().HasDatabaseName("ix_surgery_protocol_service_id");
    }
}
