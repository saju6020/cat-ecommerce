using System.Text.Json;
using CatEcommerce.Domain.Aggregates.Reporting;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Infrastructure.Persistence.ReportingViews;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations.Reporting;

/// <summary>
/// HO-RPT-001 section 8.9 / 12.1 (ADR-287) -- <c>report.report_schedule</c>. Reporting's own table (the only two it owns besides the views); no foreign key to a branch or staff account
/// (ADR-011). Enumerations are stored by <c>Value</c>, instants as <c>timestamptz</c>, the send time as <c>time</c>, the recipients as <c>uuid[]</c> and the parameters as <c>jsonb</c>.
/// </summary>
public sealed class ReportScheduleConfiguration : IEntityTypeConfiguration<ReportScheduleAggregate>
{
    private const string ReportingSchema = ReportViewConfiguration<RvStaffRow>.Schema;

    public void Configure(EntityTypeBuilder<ReportScheduleAggregate> builder)
    {
        builder.ToTable("report_schedule", ReportingSchema);
        builder.ConfigureAggregateBase();

        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(ReportScheduleRules.NameMaxLength).IsRequired();
        builder.Property(x => x.ReportKey).HasColumnName("report_key").HasMaxLength(ReportRules.KeyMaxLength).IsRequired();
        builder.Property(x => x.BranchId).HasColumnName("branch_id");
        builder.Property(x => x.Period).HasColumnName("period").HasEnumerationValue().IsRequired();
        builder.Property(x => x.Format).HasColumnName("format").HasEnumerationValue().IsRequired();
        builder.Property(x => x.Frequency).HasColumnName("frequency").HasEnumerationValue().IsRequired();
        builder.Property(x => x.DayOfWeek).HasColumnName("day_of_week").HasConversion<int?>();
        builder.Property(x => x.DayOfMonth).HasColumnName("day_of_month");
        builder.Property(x => x.LastDayOfMonth).HasColumnName("last_day_of_month").IsRequired();
        builder.Property(x => x.SendAtLocal).HasColumnName("send_at_local").HasColumnType("time without time zone").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasEnumerationValue().IsRequired();
        builder.Property(x => x.NextRunOn).HasColumnName("next_run_on").HasColumnType("timestamptz");
        builder.Property(x => x.LastRunOn).HasColumnName("last_run_on").HasColumnType("timestamptz");
        builder.Property(x => x.LastRunDueOn).HasColumnName("last_run_due_on").HasColumnType("timestamptz");

        builder.PrimitiveCollection(x => x.RecipientIds).HasColumnName("recipient_ids").UsePropertyAccessMode(PropertyAccessMode.Field);

        var comparer = new ValueComparer<IReadOnlyDictionary<string, string>>(
            (a, b) => (a ?? new Dictionary<string, string>()).OrderBy(p => p.Key).SequenceEqual((b ?? new Dictionary<string, string>()).OrderBy(p => p.Key)),
            v => v.Aggregate(0, (hash, kv) => HashCode.Combine(hash, kv.Key, kv.Value)),
            v => new Dictionary<string, string>(v));
        builder.Property(x => x.Parameters).HasColumnName("parameters").HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                v => (IReadOnlyDictionary<string, string>)(JsonSerializer.Deserialize<Dictionary<string, string>>(v, JsonSerializerOptions.Default) ?? new Dictionary<string, string>()))
            .Metadata.SetValueComparer(comparer);

        builder.HasIndex(x => new { x.Status, x.NextRunOn }).HasDatabaseName("ix_report_schedule_status_next_run_on");
    }
}

/// <summary>
/// HO-RPT-001 section 8.9 / 8.10 / 12.1 (ADR-287, ADR-289) -- <c>report.scheduled_report_run</c>. <c>ix_scheduled_report_run_slot</c> is what makes a slot run exactly once: a UNIQUE index on
/// (schedule, due time) for <c>Schedule</c> runs that are <c>Generated</c> or <c>Purged</c>. The filter is written from the enumerations, never from literals (U-X3). The file is in HO-FIL-001; this
/// row keeps only its id.
/// </summary>
public sealed class ScheduledReportRunConfiguration : IEntityTypeConfiguration<ScheduledReportRunAggregate>
{
    private const string ReportingSchema = ReportViewConfiguration<RvStaffRow>.Schema;

    public void Configure(EntityTypeBuilder<ScheduledReportRunAggregate> builder)
    {
        builder.ToTable("scheduled_report_run", ReportingSchema);
        builder.ConfigureAggregateBase();

        builder.Property(x => x.ScheduleId).HasColumnName("schedule_id").IsRequired();
        builder.Property(x => x.DueOn).HasColumnName("due_on").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.Trigger).HasColumnName("trigger").HasEnumerationValue().IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasEnumerationValue().IsRequired();
        builder.Property(x => x.PeriodFrom).HasColumnName("period_from").HasColumnType("date").IsRequired();
        builder.Property(x => x.PeriodTo).HasColumnName("period_to").HasColumnType("date").IsRequired();
        builder.Property(x => x.TimeZoneId).HasColumnName("time_zone_id").HasMaxLength(64).IsRequired();
        builder.Property(x => x.Format).HasColumnName("format").HasEnumerationValue().IsRequired();
        builder.Property(x => x.FileName).HasColumnName("file_name").HasMaxLength(200);
        builder.Property(x => x.ContentType).HasColumnName("content_type").HasMaxLength(100);
        builder.Property(x => x.ContentLength).HasColumnName("content_length").IsRequired();
        builder.Property(x => x.ContentSha256).HasColumnName("content_sha256").HasMaxLength(64);
        builder.Property(x => x.RowCount).HasColumnName("row_count").IsRequired();
        builder.Property(x => x.AttachmentOmitted).HasColumnName("attachment_omitted").IsRequired();
        builder.Property(x => x.FileId).HasColumnName("file_id");
        builder.Property(x => x.FailureCode).HasColumnName("failure_code").HasMaxLength(ReportScheduleRules.FailureCodeMaxLength);
        builder.Property(x => x.GeneratedOn).HasColumnName("generated_on").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.PurgedOn).HasColumnName("purged_on").HasColumnType("timestamptz");

        builder.PrimitiveCollection(x => x.ColumnsIncluded).HasColumnName("columns_included").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(x => new { x.ScheduleId, x.DueOn }).IsUnique().HasDatabaseName("ix_scheduled_report_run_slot")
            .HasFilter($"\"trigger\" = {ReportRunTrigger.Schedule.Value} AND status IN ({ScheduledReportRunStatus.Generated.Value}, {ScheduledReportRunStatus.Purged.Value})");
        builder.HasIndex(x => new { x.ScheduleId, x.GeneratedOn }).HasDatabaseName("ix_scheduled_report_run_schedule_generated_on");
        builder.HasIndex(x => new { x.Status, x.GeneratedOn }).HasDatabaseName("ix_scheduled_report_run_status_generated_on");
    }
}
