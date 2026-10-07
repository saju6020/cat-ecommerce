using CatEcommerce.Domain.Aggregates.Clinic;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>HO-APT-001 §8.11/§12.1.</summary>
public sealed class WaitlistEntryConfiguration : IEntityTypeConfiguration<WaitlistEntryAggregate>
{
    public void Configure(EntityTypeBuilder<WaitlistEntryAggregate> builder)
    {
        builder.ToTable("waitlist_entry", "clinic");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(x => x.CustomerId).HasColumnName("customer_id").IsRequired();
        builder.Property(x => x.CatId).HasColumnName("cat_id").IsRequired();
        builder.Property(x => x.ClinicServiceId).HasColumnName("service_id").IsRequired();
        builder.Property(x => x.LocalDate).HasColumnName("local_date").HasColumnType("date").IsRequired();
        builder.Property(x => x.PreferredPerformerId).HasColumnName("preferred_performer_id");
        builder.Property(x => x.Status).HasColumnName("status")
            .HasConversion(v => v.Value, v => Enumeration.FromValue<WaitlistStatus>(v)).IsRequired();
        builder.Property(x => x.OfferedAppointmentId).HasColumnName("offered_appointment_id");
        builder.Property(x => x.OfferExpiresOn).HasColumnName("offer_expires_on").HasColumnType("timestamptz");
        builder.Property(x => x.CreatedOn).HasColumnName("created_on").HasColumnType("timestamptz").IsRequired();

        builder.HasIndex(x => new { x.BranchId, x.ClinicServiceId, x.LocalDate, x.Status, x.CreatedOn }).HasDatabaseName("ix_waitlist_entry_offer_pass");

        // §12.1: one active (Waiting/Offered) entry per cat, service and date. Values written from
        // the enumeration, never as bare literals (U-X3's discipline).
        builder.HasIndex(x => new { x.CatId, x.ClinicServiceId, x.LocalDate }).IsUnique()
            .HasDatabaseName("ix_waitlist_active_per_cat")
            .HasFilter($"status IN ({WaitlistStatus.Waiting.Value}, {WaitlistStatus.Offered.Value})");

        builder.Property(x => x.Tags).IsRequired(false);
        builder.Property(x => x.Language).IsRequired(false);
        builder.Property(x => x.ServiceId).IsRequired(false);
        builder.Property<uint>("xmin").IsRowVersion();
        builder.Ignore(x => x.Events);
        builder.Ignore(x => x.Metadata);
    }
}
