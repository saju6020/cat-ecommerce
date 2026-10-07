using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>HO-ADM-001 §12.1.</summary>
public sealed class StaffRosterConfiguration : IEntityTypeConfiguration<StaffRosterAggregate>
{
    public void Configure(EntityTypeBuilder<StaffRosterAggregate> builder)
    {
        builder.ToTable("staff_roster", "admin");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.StaffAccountId).HasColumnName("staff_account_id").IsRequired();
        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(x => x.NextPatternEffectiveFrom).HasColumnName("next_pattern_effective_from").HasColumnType("date");

        builder.HasIndex(x => new { x.StaffAccountId, x.BranchId }).IsUnique().HasDatabaseName("ix_staff_roster_staff_branch");

        builder.OwnsMany(x => x.CurrentPattern, w =>
        {
            w.ToTable("staff_roster_current_shift", "admin");
            w.WithOwner().HasForeignKey("staff_roster_id");
            ConfigureShiftSlots(w);
        });
        builder.Metadata.FindNavigation(nameof(StaffRosterAggregate.CurrentPattern))!.SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(x => x.NextPattern, w =>
        {
            w.ToTable("staff_roster_next_shift", "admin");
            w.WithOwner().HasForeignKey("staff_roster_id");
            ConfigureShiftSlots(w);
        });
        builder.Metadata.FindNavigation(nameof(StaffRosterAggregate.NextPattern))!.SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(x => x.Tags).IsRequired(false);
        builder.Property(x => x.Language).IsRequired(false);
        builder.Property(x => x.ServiceId).IsRequired(false);
        builder.Property<uint>("xmin").IsRowVersion();
        builder.Ignore(x => x.Events);
        builder.Ignore(x => x.Metadata);
    }

    private static void ConfigureShiftSlots(OwnedNavigationBuilder<StaffRosterAggregate, ShiftSlot> w)
    {
        w.HasKey("Id");
        w.Property(p => p.Id).ValueGeneratedNever().HasColumnName("id");
        w.Property(p => p.DayOfWeek).HasColumnName("day_of_week").IsRequired();
        w.Property(p => p.Starts).HasColumnName("starts").HasColumnType("time").IsRequired();
        w.Property(p => p.Ends).HasColumnName("ends").HasColumnType("time").IsRequired();
        w.Property(p => p.DefaultRoomId).HasColumnName("default_room_id");
    }
}
