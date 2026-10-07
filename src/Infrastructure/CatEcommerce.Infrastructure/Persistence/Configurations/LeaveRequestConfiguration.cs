using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

public sealed class LeaveRequestConfiguration : IEntityTypeConfiguration<LeaveRequestAggregate>
{
    public void Configure(EntityTypeBuilder<LeaveRequestAggregate> builder)
    {
        builder.ToTable("leave_request", "admin");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.StaffAccountId).HasColumnName("staff_account_id").IsRequired();
        builder.Property(x => x.StartsOn).HasColumnName("starts_on").HasColumnType("date").IsRequired();
        builder.Property(x => x.EndsOn).HasColumnName("ends_on").HasColumnType("date").IsRequired();
        builder.Property(x => x.Kind).HasColumnName("kind").HasConversion(k => k.Value, v => Enumeration.FromValue<LeaveKind>(v)).IsRequired();
        builder.Property(x => x.Note).HasColumnName("note").HasMaxLength(500);
        builder.Property(x => x.Status).HasColumnName("status").HasConversion(k => k.Value, v => Enumeration.FromValue<LeaveStatus>(v)).IsRequired();
        builder.Property(x => x.DecidedBy).HasColumnName("decided_by");
        builder.Property(x => x.DecidedOn).HasColumnName("decided_on").HasColumnType("timestamptz");
        builder.Property(x => x.DecisionNote).HasColumnName("decision_note").HasMaxLength(500);

        builder.HasIndex(x => new { x.StaffAccountId, x.StartsOn, x.EndsOn }).HasDatabaseName("ix_leave_request_staff_range");
        builder.HasIndex(x => x.Status).HasDatabaseName("ix_leave_request_status");

        builder.Property(x => x.Tags).IsRequired(false);
        builder.Property(x => x.Language).IsRequired(false);
        builder.Property(x => x.ServiceId).IsRequired(false);
        builder.Property<uint>("xmin").IsRowVersion();
        builder.Ignore(x => x.Events);
        builder.Ignore(x => x.Metadata);
    }
}
