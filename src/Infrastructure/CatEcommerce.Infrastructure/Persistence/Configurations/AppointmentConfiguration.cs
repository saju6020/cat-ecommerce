using CatEcommerce.Domain.Aggregates.Clinic;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>HO-APT-001 §12.1. The three exclusion constraints are raw SQL in the
/// <c>AddClinicAppointments</c> migration -- EF does not model them (.ai/persistence-ef-core.md).</summary>
public sealed class AppointmentConfiguration : IEntityTypeConfiguration<AppointmentAggregate>
{
    public void Configure(EntityTypeBuilder<AppointmentAggregate> builder)
    {
        builder.ToTable("appointment", "clinic");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(x => x.CustomerId).HasColumnName("customer_id").IsRequired();
        builder.Property(x => x.CatId).HasColumnName("cat_id").IsRequired();
        builder.Property(x => x.ClinicServiceId).HasColumnName("service_id").IsRequired();

        builder.OwnsOne(x => x.ServiceSnapshot, s =>
        {
            s.Property(p => p.Name).HasColumnName("service_name").HasMaxLength(200).IsRequired();
            s.Property(p => p.Category).HasColumnName("service_category")
                .HasConversion(v => v.Value, v => Enumeration.FromValue<ServiceCategory>(v)).IsRequired();
            s.Property(p => p.DurationMinutes).HasColumnName("service_duration_minutes").IsRequired();
            s.OwnsOne(p => p.Price, price =>
            {
                price.Property(m => m.Amount).HasColumnName("price_amount").HasColumnType("numeric(12,2)").IsRequired();
                price.Property(m => m.CurrencyCode).HasColumnName("price_currency_code").HasColumnType("char(3)").IsRequired();
            });
        });
        builder.Navigation(x => x.ServiceSnapshot).IsRequired();

        builder.Property(x => x.PerformerId).HasColumnName("performer_id");
        builder.Property(x => x.RoomId).HasColumnName("room_id");
        builder.Property(x => x.StartsOn).HasColumnName("starts_on").HasColumnType("timestamptz");
        builder.Property(x => x.EndsOn).HasColumnName("ends_on").HasColumnType("timestamptz");
        builder.Property(x => x.IsWalkIn).HasColumnName("is_walk_in").IsRequired();
        builder.Property(x => x.Channel).HasColumnName("channel")
            .HasConversion(v => v.Value, v => Enumeration.FromValue<BookingChannel>(v)).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status")
            .HasConversion(v => v.Value, v => Enumeration.FromValue<AppointmentStatus>(v)).IsRequired();
        builder.Property(x => x.HoldExpiresOn).HasColumnName("hold_expires_on").HasColumnType("timestamptz");
        builder.Property(x => x.WaitlistEntryId).HasColumnName("waitlist_entry_id");
        builder.Property(x => x.ReasonForVisit).HasColumnName("reason_for_visit").HasMaxLength(AppointmentRules.MaxReasonLength);
        builder.Property(x => x.TriagePriority).HasColumnName("triage_priority")
            .HasConversion(v => v.Value, v => Enumeration.FromValue<TriagePriority>(v)).IsRequired();
        builder.Property(x => x.PresentingComplaint).HasColumnName("presenting_complaint").HasMaxLength(AppointmentRules.MaxReasonLength);

        builder.Property(x => x.CheckedInOn).HasColumnName("checked_in_on").HasColumnType("timestamptz");
        builder.Property(x => x.StartedOn).HasColumnName("started_on").HasColumnType("timestamptz");
        builder.Property(x => x.CompletedOn).HasColumnName("completed_on").HasColumnType("timestamptz");
        builder.Property(x => x.CancelledOn).HasColumnName("cancelled_on").HasColumnType("timestamptz");
        builder.Property(x => x.CancelledBy).HasColumnName("cancelled_by");
        builder.Property(x => x.CancellationReason).HasColumnName("cancellation_reason").HasMaxLength(AppointmentRules.MaxReasonLength);
        builder.Property(x => x.CancelledByCustomer).HasColumnName("cancelled_by_customer").IsRequired();
        builder.Property(x => x.NoShowRecordedOn).HasColumnName("no_show_recorded_on").HasColumnType("timestamptz");
        builder.Property(x => x.NoShowRecordedBy).HasColumnName("no_show_recorded_by");
        builder.Property(x => x.RescheduleCount).HasColumnName("reschedule_count").IsRequired();
        builder.Property(x => x.ScheduleStamp).HasColumnName("schedule_stamp").HasMaxLength(64);

        builder.Property(x => x.NeedsRescheduling).HasColumnName("needs_rescheduling").IsRequired();
        builder.Property(x => x.RescheduleFlagReason).HasColumnName("reschedule_flag_reason")
            .HasConversion(v => v == null ? (int?)null : v.Value, v => v == null ? null : Enumeration.FromValue<RescheduleFlagReason>(v.Value));
        builder.Property(x => x.RescheduleFlagSourceId).HasColumnName("reschedule_flag_source_id");

        // HO-SRG-001 section 11.7 G1/G3 (ADR-158, ADR-159): the surgery this appointment holds time for, and the assistant.
        builder.Property(x => x.SurgeryId).HasColumnName("surgery_id");
        builder.Property(x => x.AssistantId).HasColumnName("assistant_id");

        builder.OwnsMany(x => x.StatusHistory, h =>
        {
            h.ToTable("appointment_status_change", "clinic");
            h.WithOwner().HasForeignKey("appointment_id");
            h.HasKey("Id");
            h.Property(p => p.Id).ValueGeneratedNever().HasColumnName("id");
            h.Property(p => p.From).HasColumnName("from_status")
                .HasConversion(v => v.Value, v => Enumeration.FromValue<AppointmentStatus>(v)).IsRequired();
            h.Property(p => p.To).HasColumnName("to_status")
                .HasConversion(v => v.Value, v => Enumeration.FromValue<AppointmentStatus>(v)).IsRequired();
            h.Property(p => p.ChangedOn).HasColumnName("changed_on").HasColumnType("timestamptz").IsRequired();
            h.Property(p => p.ChangedBy).HasColumnName("changed_by").IsRequired();
            h.Property(p => p.Sequence).HasColumnName("sequence").HasDefaultValue(0).IsRequired();
        });
        builder.Metadata.FindNavigation(nameof(AppointmentAggregate.StatusHistory))!.SetPropertyAccessMode(PropertyAccessMode.Field);

        // HO-SRG-001 section 8.4 (ADR-159): the staff-time rows behind ex_appointment_staff_time (raw SQL in the AddAppointmentStaffTime migration).
        builder.OwnsMany(x => x.StaffTime, h =>
        {
            h.ToTable("appointment_staff_time", "clinic");
            h.WithOwner().HasForeignKey("appointment_id");
            h.HasKey("Id");
            h.Property(p => p.Id).ValueGeneratedNever().HasColumnName("id");
            h.Property(p => p.Role).HasColumnName("role")
                .HasConversion(v => v.Value, v => Enumeration.FromValue<StaffTimeRole>(v)).IsRequired();
            h.Property(p => p.StaffAccountId).HasColumnName("staff_account_id").IsRequired();
            h.Property(p => p.StartsOn).HasColumnName("starts_on").HasColumnType("timestamptz").IsRequired();
            h.Property(p => p.EndsOn).HasColumnName("ends_on").HasColumnType("timestamptz").IsRequired();
            h.Property(p => p.Occupies).HasColumnName("occupies").IsRequired();
            h.HasIndex("appointment_id").HasDatabaseName("ix_appointment_staff_time_appointment");
            h.HasIndex(p => new { p.StaffAccountId, p.StartsOn }).HasDatabaseName("ix_appointment_staff_time_staff_starts_on");
        });
        builder.Metadata.FindNavigation(nameof(AppointmentAggregate.StaffTime))!.SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(x => new { x.BranchId, x.StartsOn }).HasDatabaseName("ix_appointment_branch_starts_on");
        builder.HasIndex(x => new { x.PerformerId, x.StartsOn }).HasDatabaseName("ix_appointment_performer_starts_on");
        builder.HasIndex(x => new { x.RoomId, x.StartsOn }).HasDatabaseName("ix_appointment_room_starts_on");
        builder.HasIndex(x => new { x.CustomerId, x.StartsOn }).HasDatabaseName("ix_appointment_customer_starts_on");
        builder.HasIndex(x => new { x.CustomerId, x.Status }).HasDatabaseName("ix_appointment_customer_status");
        builder.HasIndex(x => new { x.BranchId, x.Status }).HasDatabaseName("ix_appointment_branch_status");
        builder.HasIndex(x => new { x.Status, x.HoldExpiresOn }).HasDatabaseName("ix_appointment_status_hold_expires_on");
        builder.HasIndex(x => x.SurgeryId).HasDatabaseName("ix_appointment_surgery_id").HasFilter("surgery_id IS NOT NULL");
        builder.HasIndex(x => new { x.NeedsRescheduling, x.BranchId }).HasDatabaseName("ix_appointment_needs_rescheduling_branch").HasFilter("needs_rescheduling");

        builder.Property(x => x.Tags).IsRequired(false);
        builder.Property(x => x.Language).IsRequired(false);
        builder.Property(x => x.ServiceId).IsRequired(false);
        builder.Property<uint>("xmin").IsRowVersion();
        builder.Ignore(x => x.Events);
        builder.Ignore(x => x.Metadata);
    }
}
