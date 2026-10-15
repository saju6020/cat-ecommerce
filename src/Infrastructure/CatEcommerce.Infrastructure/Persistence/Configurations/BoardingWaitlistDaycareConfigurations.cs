using CatEcommerce.Domain.Aggregates.Boarding;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>HO-BRD-001 section 8.18 / 12.1. <c>ix_boarding_waitlist_active</c> allows one LIVE entry per customer and range; its predicate is built from the enumeration values, never literals (U-X3).</summary>
public sealed class BoardingWaitlistEntryConfiguration : IEntityTypeConfiguration<BoardingWaitlistEntryAggregate>
{
    public void Configure(EntityTypeBuilder<BoardingWaitlistEntryAggregate> builder)
    {
        builder.ToTable("waitlist_entry", "boarding");
        builder.ConfigureAggregateBase();

        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(x => x.CustomerId).HasColumnName("customer_id").IsRequired();
        builder.Property(x => x.ArrivalOn).HasColumnName("arrival_on").HasColumnType("date").IsRequired();
        builder.Property(x => x.DepartureOn).HasColumnName("departure_on").HasColumnType("date").IsRequired();
        builder.Property(x => x.CatCount).HasColumnName("cat_count").IsRequired();
        builder.Property(x => x.UnitType).HasColumnName("unit_type")
            .HasConversion(v => v == null ? (int?)null : v.Value, v => v == null ? null : Enumeration.FromValue<BoardingUnitType>(v.Value));
        builder.Property(x => x.Status).HasColumnName("status").HasEnumerationValue().IsRequired();
        builder.Property(x => x.JoinedOn).HasColumnName("joined_on").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.NotifiedOn).HasColumnName("notified_on").HasColumnType("timestamptz");
        builder.Property(x => x.EndedOn).HasColumnName("ended_on").HasColumnType("timestamptz");
        builder.Ignore(x => x.IsLive);

        builder.HasIndex(x => new { x.BranchId, x.ArrivalOn, x.Status, x.JoinedOn }).HasDatabaseName("ix_waitlist_entry_pass");
        builder.HasIndex(x => new { x.CustomerId, x.ArrivalOn, x.DepartureOn }).IsUnique().HasDatabaseName("ix_boarding_waitlist_active")
            .HasFilter($"status IN ({BoardingWaitlistStatus.Waiting.Value}, {BoardingWaitlistStatus.Notified.Value})");
    }
}

public sealed class DaycareRateCardConfiguration : IEntityTypeConfiguration<DaycareRateCardAggregate>
{
    public void Configure(EntityTypeBuilder<DaycareRateCardAggregate> builder)
    {
        builder.ToTable("daycare_rate_card", "boarding");
        builder.ConfigureAggregateBase();

        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(x => x.HourlyRate).HasColumnName("hourly_rate").HasColumnType("numeric(12,2)").IsRequired();
        builder.Property(x => x.DayRate).HasColumnName("day_rate").HasColumnType("numeric(12,2)").IsRequired();
        builder.Property(x => x.TaxClassId).HasColumnName("tax_class_id").IsRequired();
        builder.Property(x => x.CurrencyCode).HasColumnName("currency_code").HasColumnType("char(3)").IsRequired();
        builder.Property(x => x.EffectiveFrom).HasColumnName("effective_from").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.SetBy).HasColumnName("set_by").IsRequired();

        builder.HasIndex(x => new { x.BranchId, x.EffectiveFrom }).IsUnique().HasDatabaseName("ix_daycare_rate_card_effective");
    }
}

/// <summary><c>ix_daycare_slot</c> -- the race on the last slot of a day: unique per branch, day and slot among visits that are not cancelled.</summary>
public sealed class DaycareVisitConfiguration : IEntityTypeConfiguration<DaycareVisitAggregate>
{
    public void Configure(EntityTypeBuilder<DaycareVisitAggregate> builder)
    {
        builder.ToTable("daycare_visit", "boarding");
        builder.ConfigureAggregateBase();

        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(x => x.CustomerId).HasColumnName("customer_id").IsRequired();
        builder.Property(x => x.CatId).HasColumnName("cat_id").IsRequired();
        builder.Property(x => x.CatName).HasColumnName("cat_name").HasMaxLength(100).IsRequired();
        builder.Property(x => x.LocalDate).HasColumnName("local_date").HasColumnType("date").IsRequired();
        builder.Property(x => x.SlotNumber).HasColumnName("slot_number").IsRequired();
        builder.Property(x => x.Basis).HasColumnName("basis").HasEnumerationValue().IsRequired();
        builder.Property(x => x.ExpectedHours).HasColumnName("expected_hours");
        builder.Property(x => x.Status).HasColumnName("status").HasEnumerationValue().IsRequired();
        builder.Property(x => x.HourlyRate).HasColumnName("hourly_rate").HasColumnType("numeric(12,2)").IsRequired();
        builder.Property(x => x.DayRate).HasColumnName("day_rate").HasColumnType("numeric(12,2)").IsRequired();
        builder.Property(x => x.TaxClassId).HasColumnName("tax_class_id").IsRequired();
        builder.Property(x => x.CurrencyCode).HasColumnName("currency_code").HasColumnType("char(3)").IsRequired();
        builder.Property(x => x.BookedOn).HasColumnName("booked_on").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.CheckedInOn).HasColumnName("checked_in_on").HasColumnType("timestamptz");
        builder.Property(x => x.CheckedOutOn).HasColumnName("checked_out_on").HasColumnType("timestamptz");
        builder.Property(x => x.ChargeAmount).HasColumnName("charge_amount").HasColumnType("numeric(12,2)");

        builder.HasIndex(x => new { x.BranchId, x.LocalDate, x.SlotNumber }).IsUnique().HasDatabaseName("ix_daycare_slot")
            .HasFilter($"status <> {DaycareVisitStatus.Cancelled.Value}");
        builder.HasIndex(x => new { x.CustomerId, x.LocalDate }).HasDatabaseName("ix_daycare_visit_customer_date");
        builder.HasIndex(x => new { x.CatId, x.LocalDate }).HasDatabaseName("ix_daycare_visit_cat_date");
    }
}
