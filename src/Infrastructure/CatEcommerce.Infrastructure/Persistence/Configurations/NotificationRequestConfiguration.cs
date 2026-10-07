using System.Text.Json;
using CatEcommerce.Domain.Aggregates.Notification;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>HO-NTF-001 §8.4/§12.1 -- the outbox item. No domain events, no owned events to ignore
/// beyond the base <c>Events</c> property.</summary>
public sealed class NotificationRequestConfiguration : IEntityTypeConfiguration<NotificationRequestAggregate>
{
    public void Configure(EntityTypeBuilder<NotificationRequestAggregate> builder)
    {
        builder.ToTable("notification_request", "notify");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.TemplateKey).HasColumnName("template_key").HasMaxLength(80).IsRequired();
        builder.Property(x => x.RecipientKind).HasColumnName("recipient_kind")
            .HasConversion(v => v.Value, v => Enumeration.FromValue<NotificationRecipientKind>(v)).IsRequired();
        builder.Property(x => x.RecipientId).HasColumnName("recipient_id").IsRequired();

        var dictionaryConverter = new ValueConverter<IReadOnlyDictionary<string, string>, string>(
            v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
            v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, JsonSerializerOptions.Default) ?? new Dictionary<string, string>());

        var dictionaryComparer = new ValueComparer<IReadOnlyDictionary<string, string>>(
            (a, b) => (a ?? new Dictionary<string, string>()).SequenceEqual(b ?? new Dictionary<string, string>()),
            v => v.Aggregate(0, (hash, kv) => HashCode.Combine(hash, kv.Key, kv.Value)),
            v => new Dictionary<string, string>(v));

        builder.Property(x => x.Variables).HasColumnName("variables").HasColumnType("jsonb")
            .HasConversion(dictionaryConverter).Metadata.SetValueComparer(dictionaryComparer);

        builder.Property(x => x.ChannelHint).HasColumnName("channel_hint")
            .HasConversion(v => v == null ? (int?)null : v.Value, v => v == null ? null : Enumeration.FromValue<NotificationChannel>(v.Value));
        builder.Property(x => x.SubjectKind).HasColumnName("subject_kind")
            .HasConversion(v => v == null ? (int?)null : v.Value, v => v == null ? null : Enumeration.FromValue<NotificationSubjectKind>(v.Value));
        builder.Property(x => x.SubjectId).HasColumnName("subject_id");
        builder.Property(x => x.SubjectRef).HasColumnName("subject_ref");
        builder.Property(x => x.SubjectDueOn).HasColumnName("subject_due_on").HasColumnType("date");
        builder.Property(x => x.BranchId).HasColumnName("branch_id");
        builder.Property(x => x.DeduplicationKey).HasColumnName("deduplication_key").HasMaxLength(200);
        builder.HasIndex(x => x.DeduplicationKey).IsUnique().HasDatabaseName("ix_notification_request_dedup").HasFilter("deduplication_key IS NOT NULL");

        builder.Property(x => x.NotAfter).HasColumnName("not_after").HasColumnType("timestamptz");

        // HO-APT-001 §8.9 amendment (§1 Amended, HO-NTF-001): SendAfter/SubjectStamp. Migration
        // AddNotificationRequestScheduling.
        builder.Property(x => x.SendAfter).HasColumnName("send_after").HasColumnType("timestamptz");
        builder.Property(x => x.SubjectStamp).HasColumnName("subject_stamp").HasMaxLength(64);

        builder.Property(x => x.ContainsSecret).HasColumnName("contains_secret").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status")
            .HasConversion(v => v.Value, v => Enumeration.FromValue<NotificationRequestStatus>(v)).IsRequired();
        builder.Property(x => x.SuppressionReason).HasColumnName("suppression_reason")
            .HasConversion(v => v == null ? (int?)null : v.Value, v => v == null ? null : Enumeration.FromValue<NotificationSuppressionReason>(v.Value));
        builder.HasIndex(x => new { x.Status, x.RequestedOn }).HasDatabaseName("ix_notification_request_status_requested_on");

        builder.Property(x => x.RequestedOn).HasColumnName("requested_on").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.ProcessedOn).HasColumnName("processed_on").HasColumnType("timestamptz");
        builder.Property(x => x.RequestedBy).HasColumnName("requested_by").IsRequired();
        builder.Property(x => x.CorrelationId).HasColumnName("correlation_id").IsRequired();
        builder.Property(x => x.VariablesRedactedOn).HasColumnName("variables_redacted_on").HasColumnType("timestamptz");

        // Gate fix 2026-09-27 (SECURITY/CODE_REVIEW second pass, MEDIUM). See
        // NotificationRequestAggregate.RecordFailedAttempt and NotificationWorkQueue's ordering.
        builder.Property(x => x.AttemptCount).HasColumnName("attempt_count").IsRequired().HasDefaultValue(0);
        builder.Property(x => x.LastAttemptOn).HasColumnName("last_attempt_on").HasColumnType("timestamptz");

        builder.Property(x => x.Tags).IsRequired(false);
        builder.Property(x => x.Language).IsRequired(false);
        builder.Property(x => x.ServiceId).IsRequired(false);

        builder.Property<uint>("xmin").IsRowVersion();
        builder.Ignore(x => x.Events);
        builder.Ignore(x => x.Metadata);
    }
}
