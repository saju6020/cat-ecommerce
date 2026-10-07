using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

public sealed class RosterExceptionConfiguration : IEntityTypeConfiguration<RosterExceptionAggregate>
{
    public void Configure(EntityTypeBuilder<RosterExceptionAggregate> builder)
    {
        builder.ToTable("roster_exception", "admin");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.StaffAccountId).HasColumnName("staff_account_id").IsRequired();
        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(x => x.Date).HasColumnName("date").HasColumnType("date").IsRequired();
        builder.Property(x => x.Kind).HasColumnName("kind").HasConversion(k => k.Value, v => Enumeration.FromValue<RosterExceptionKind>(v)).IsRequired();
        builder.Property(x => x.Starts).HasColumnName("starts").HasColumnType("time").IsRequired();
        builder.Property(x => x.Ends).HasColumnName("ends").HasColumnType("time").IsRequired();
        builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(200).IsRequired();
        builder.Property(x => x.IsCancelled).HasColumnName("is_cancelled").IsRequired();

        builder.HasIndex(x => new { x.BranchId, x.Date }).HasDatabaseName("ix_roster_exception_branch_date");
        builder.HasIndex(x => new { x.StaffAccountId, x.Date }).HasDatabaseName("ix_roster_exception_staff_date");

        builder.Property(x => x.Tags).IsRequired(false);
        builder.Property(x => x.Language).IsRequired(false);
        builder.Property(x => x.ServiceId).IsRequired(false);
        builder.Property<uint>("xmin").IsRowVersion();
        builder.Ignore(x => x.Events);
        builder.Ignore(x => x.Metadata);
    }
}
