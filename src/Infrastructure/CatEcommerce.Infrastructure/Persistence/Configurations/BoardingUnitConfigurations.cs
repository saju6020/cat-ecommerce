using CatEcommerce.Domain.Aggregates.Boarding;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>HO-BRD-001 section 12.1 -- schema <c>boarding</c>. The unit's (branch, code) pair is unique (<c>ix_boarding_unit_branch_code</c>, mapped to a 400
/// by the constraint filter).</summary>
public sealed class BoardingUnitConfiguration : IEntityTypeConfiguration<BoardingUnitAggregate>
{
    public void Configure(EntityTypeBuilder<BoardingUnitAggregate> builder)
    {
        builder.ToTable("unit", "boarding");
        builder.ConfigureAggregateBase();

        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(StayRules.MaxUnitCodeLength).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(StayRules.MaxUnitNameLength).IsRequired();
        builder.Property(x => x.Type).HasColumnName("unit_type").HasEnumerationValue().IsRequired();
        builder.Property(x => x.CapacityCats).HasColumnName("capacity_cats").IsRequired();
        builder.Property(x => x.Block).HasColumnName("block").HasMaxLength(StayRules.MaxUnitBlockLength);
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.SortOrder).HasColumnName("sort_order").IsRequired();

        builder.HasIndex(x => new { x.BranchId, x.Code }).IsUnique().HasDatabaseName("ix_boarding_unit_branch_code");
        builder.HasIndex(x => new { x.BranchId, x.Type, x.IsActive }).HasDatabaseName("ix_boarding_unit_branch_type");
    }
}

/// <summary>HO-BRD-001 section 12.1. The exclusion constraint <c>ex_unit_maintenance</c> is raw SQL in the <c>AddBoardingUnits</c> migration (EF does not model
/// it); it reads <c>occupied_from</c>/<c>occupied_to</c>, which the aggregate derives from the local dates.</summary>
public sealed class UnitMaintenanceBlockConfiguration : IEntityTypeConfiguration<UnitMaintenanceBlockAggregate>
{
    public void Configure(EntityTypeBuilder<UnitMaintenanceBlockAggregate> builder)
    {
        builder.ToTable("unit_maintenance_block", "boarding");
        builder.ConfigureAggregateBase();

        builder.Property(x => x.UnitId).HasColumnName("unit_id").IsRequired();
        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(x => x.StartsOn).HasColumnName("starts_on").HasColumnType("date").IsRequired();
        builder.Property(x => x.EndsOn).HasColumnName("ends_on").HasColumnType("date").IsRequired();
        builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(StayRules.MaxMaintenanceReasonLength).IsRequired();
        builder.Property(x => x.OccupiedFrom).HasColumnName("occupied_from").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.OccupiedTo).HasColumnName("occupied_to").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.IsCancelled).HasColumnName("is_cancelled").IsRequired();

        builder.HasIndex(x => new { x.UnitId, x.OccupiedFrom }).HasDatabaseName("ix_unit_maintenance_block_unit_from");
        builder.HasIndex(x => new { x.BranchId, x.StartsOn }).HasDatabaseName("ix_unit_maintenance_block_branch_starts_on");
    }
}

/// <summary>HO-BRD-001 section 12.1 -- one card per (branch, unit type, effective-from), append-only: <c>ix_boarding_rate_card_type_effective</c>.</summary>
public sealed class BoardingRateCardConfiguration : IEntityTypeConfiguration<BoardingRateCardAggregate>
{
    public void Configure(EntityTypeBuilder<BoardingRateCardAggregate> builder)
    {
        builder.ToTable("rate_card", "boarding");
        builder.ConfigureAggregateBase();

        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(x => x.UnitType).HasColumnName("unit_type").HasEnumerationValue().IsRequired();
        builder.Property(x => x.NightlyRate).HasColumnName("nightly_rate").HasColumnType("numeric(12,2)").IsRequired();
        builder.Property(x => x.WeeklyRate).HasColumnName("weekly_rate").HasColumnType("numeric(12,2)").IsRequired();
        builder.Property(x => x.MonthlyRate).HasColumnName("monthly_rate").HasColumnType("numeric(12,2)").IsRequired();
        builder.Property(x => x.AdditionalCatNightlyRate).HasColumnName("additional_cat_nightly_rate").HasColumnType("numeric(12,2)").IsRequired();
        builder.Property(x => x.TaxClassId).HasColumnName("tax_class_id").IsRequired();
        builder.Property(x => x.CurrencyCode).HasColumnName("currency_code").HasColumnType("char(3)").IsRequired();
        builder.Property(x => x.EffectiveFrom).HasColumnName("effective_from").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.SetBy).HasColumnName("set_by").IsRequired();

        builder.HasIndex(x => new { x.BranchId, x.UnitType, x.EffectiveFrom }).IsUnique().HasDatabaseName("ix_boarding_rate_card_type_effective");
    }
}

/// <summary>HO-BRD-001 section 12.1 -- a branch's care routine; its id is the branch id. The times are owned and replaced wholesale.</summary>
public sealed class BoardingCareRoutineConfiguration : IEntityTypeConfiguration<BoardingCareRoutineAggregate>
{
    public void Configure(EntityTypeBuilder<BoardingCareRoutineAggregate> builder)
    {
        builder.ToTable("care_routine", "boarding");
        builder.ConfigureAggregateBase();
        builder.Ignore(x => x.BranchId);

        builder.OwnsMany(x => x.Times, t =>
        {
            t.ToTable("care_routine_time", "boarding");
            t.WithOwner().HasForeignKey("routine_id");
            t.HasKey("Id");
            t.Property(p => p.Id).HasColumnName("id").ValueGeneratedNever();
            t.Property(p => p.Kind).HasColumnName("kind").HasEnumerationValue().IsRequired();
            t.Property(p => p.LocalTime).HasColumnName("local_time").HasColumnType("time").IsRequired();
            t.HasIndex("routine_id").HasDatabaseName("ix_care_routine_time_routine");
        });
        builder.Metadata.FindNavigation(nameof(BoardingCareRoutineAggregate.Times))!.SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
