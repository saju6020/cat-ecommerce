using CatEcommerce.Domain.Aggregates.Notification;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>HO-NTF-001 §8.6/§12.1 -- the outbox item, the delivery log and (for InApp) the inbox
/// item, all in one row.</summary>
public sealed class NotificationConfiguration : IEntityTypeConfiguration<NotificationAggregate>
{
    public void Configure(EntityTypeBuilder<NotificationAggregate> builder)
    {
        builder.ToTable("notification", "notify");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.RequestId).HasColumnName("request_id").IsRequired();
        builder.Property(x => x.TemplateKey).HasColumnName("template_key").HasMaxLength(80).IsRequired();
        builder.Property(x => x.TemplateVersion).HasColumnName("template_version").IsRequired();
        builder.Property(x => x.Locale).HasColumnName("locale").HasMaxLength(10).IsRequired();
        builder.Property(x => x.Category).HasColumnName("category")
            .HasConversion(v => v.Value, v => Enumeration.FromValue<NotificationCategory>(v)).IsRequired();
        builder.Property(x => x.Urgency).HasColumnName("urgency")
            .HasConversion(v => v.Value, v => Enumeration.FromValue<NotificationUrgency>(v)).IsRequired();
        builder.Property(x => x.ContainsSecret).HasColumnName("contains_secret").IsRequired();
        builder.Property(x => x.RecipientKind).HasColumnName("recipient_kind")
            .HasConversion(v => v.Value, v => Enumeration.FromValue<NotificationRecipientKind>(v)).IsRequired();
        builder.Property(x => x.RecipientId).HasColumnName("recipient_id").IsRequired();

        builder.Property(x => x.SubjectKind).HasColumnName("subject_kind")
            .HasConversion(v => v == null ? (int?)null : v.Value, v => v == null ? null : Enumeration.FromValue<NotificationSubjectKind>(v.Value));
        builder.Property(x => x.SubjectId).HasColumnName("subject_id");
        builder.Property(x => x.SubjectRef).HasColumnName("subject_ref");
        builder.Property(x => x.SubjectDueOn).HasColumnName("subject_due_on").HasColumnType("date");

        // HO-APT-001 §8.9 amendment (§1 Amended, HO-NTF-001): carried onto the notification so the
        // guard can be re-checked immediately before each delivery attempt. Migration
        // AddNotificationRequestScheduling.
        builder.Property(x => x.SubjectStamp).HasColumnName("subject_stamp").HasMaxLength(64);

        builder.Property(x => x.BranchId).HasColumnName("branch_id");

        builder.Property(x => x.Channel).HasColumnName("channel")
            .HasConversion(v => v.Value, v => Enumeration.FromValue<NotificationChannel>(v)).IsRequired();
        builder.Property(x => x.Destination).HasColumnName("destination").HasMaxLength(256);
        builder.Property(x => x.Subject).HasColumnName("subject").HasMaxLength(150);
        builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(100);
        builder.Property(x => x.Body).HasColumnName("body").HasMaxLength(10_000).IsRequired();
        builder.Property(x => x.HtmlBody).HasColumnName("html_body").HasColumnType("text");
        builder.Property(x => x.LinkPath).HasColumnName("link_path").HasMaxLength(500);

        builder.Property(x => x.Status).HasColumnName("status")
            .HasConversion(v => v.Value, v => Enumeration.FromValue<NotificationStatus>(v)).IsRequired();
        builder.Property(x => x.NotBefore).HasColumnName("not_before").HasColumnType("timestamptz");
        builder.Property(x => x.DeferredForQuietHours).HasColumnName("deferred_for_quiet_hours").IsRequired();
        builder.Property(x => x.NotAfter).HasColumnName("not_after").HasColumnType("timestamptz");
        builder.Property(x => x.NextAttemptOn).HasColumnName("next_attempt_on").HasColumnType("timestamptz");
        builder.Property(x => x.LeaseExpiresOn).HasColumnName("lease_expires_on").HasColumnType("timestamptz");
        builder.Property(x => x.AttemptCount).HasColumnName("attempt_count").IsRequired();
        builder.Property(x => x.ClaimCount).HasColumnName("claim_count").HasDefaultValue(0).IsRequired();

        builder.OwnsMany(x => x.Attempts, attempt =>
        {
            attempt.ToTable("notification_attempt", "notify");
            attempt.WithOwner().HasForeignKey("notification_id");
            attempt.HasKey("Id");
            attempt.Property(a => a.Id).ValueGeneratedNever();
            attempt.Property(a => a.AttemptedOn).HasColumnName("attempted_on").HasColumnType("timestamptz").IsRequired();
            attempt.Property(a => a.Outcome).HasColumnName("outcome")
                .HasConversion(v => v.Value, v => Enumeration.FromValue<SendOutcomeKind>(v)).IsRequired();
            attempt.Property(a => a.ProviderName).HasColumnName("provider_name").HasMaxLength(100).IsRequired();
            attempt.Property(a => a.ProviderMessageId).HasColumnName("provider_message_id").HasMaxLength(200);
            attempt.Property(a => a.ProviderResponse).HasColumnName("provider_response").HasMaxLength(1000);
        });
        builder.Navigation(x => x.Attempts).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(x => x.FallbackFromId).HasColumnName("fallback_from_id");
        builder.Property(x => x.ResendOfId).HasColumnName("resend_of_id");
        builder.Property(x => x.ResentBy).HasColumnName("resent_by");
        builder.Property(x => x.ResendOrdinal).HasColumnName("resend_ordinal");
        // Gate fix 2026-09-27 (SECURITY LOW): enforces the resend limit atomically at the database
        // -- a count-then-insert race in the handler could otherwise let N concurrent resends all
        // pass the same "count < MaxResendsPerNotification" check before any of them commits.
        builder.HasIndex(x => new { x.ResendOfId, x.ResendOrdinal }).IsUnique()
            .HasDatabaseName("ix_notification_resend_of_id_ordinal").HasFilter("resend_of_id IS NOT NULL");
        builder.Property(x => x.UnsubscribeTokenHash).HasColumnName("unsubscribe_token_hash").HasMaxLength(100);
        builder.HasIndex(x => x.UnsubscribeTokenHash).IsUnique().HasDatabaseName("ix_notification_unsubscribe_token_hash").HasFilter("unsubscribe_token_hash IS NOT NULL");
        builder.Property(x => x.UnsubscribeLink).HasColumnName("unsubscribe_link").HasMaxLength(500);

        builder.Property(x => x.ReadOn).HasColumnName("read_on").HasColumnType("timestamptz");
        builder.Property(x => x.CreatedOn).HasColumnName("created_on").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.SentOn).HasColumnName("sent_on").HasColumnType("timestamptz");
        builder.Property(x => x.ClosedOn).HasColumnName("closed_on").HasColumnType("timestamptz");
        builder.Property(x => x.CloseReason).HasColumnName("close_reason")
            .HasConversion(v => v == null ? (int?)null : v.Value, v => v == null ? null : Enumeration.FromValue<NotificationCloseReason>(v.Value));
        builder.Property(x => x.RedactedOn).HasColumnName("redacted_on").HasColumnType("timestamptz");
        // Gate fix 2026-09-27 (SECURITY/ARCHITECTURE/CODE_REVIEW HIGH, ADR-041/A16).
        builder.Property(x => x.RedactedReason).HasColumnName("redacted_reason")
            .HasConversion(v => v == null ? (int?)null : v.Value, v => v == null ? null : Enumeration.FromValue<NotificationRedactionReason>(v.Value));
        builder.Property(x => x.AnonymisedOn).HasColumnName("anonymised_on").HasColumnType("timestamptz");

        builder.HasIndex(x => new { x.Status, x.NotBefore, x.NextAttemptOn }).HasDatabaseName("ix_notification_status_notbefore_nextattempt");
        builder.HasIndex(x => new { x.RecipientId, x.Channel, x.ReadOn, x.CreatedOn }).HasDatabaseName("ix_notification_inbox");
        builder.HasIndex(x => new { x.RecipientId, x.CreatedOn }).HasDatabaseName("ix_notification_recipient_created");
        builder.HasIndex(x => new { x.Destination, x.CreatedOn }).HasDatabaseName("ix_notification_destination_created");
        builder.HasIndex(x => new { x.TemplateKey, x.CreatedOn }).HasDatabaseName("ix_notification_templatekey_created");
        builder.HasIndex(x => x.CreatedOn).HasDatabaseName("ix_notification_created");
        builder.HasIndex(x => x.RequestId).HasDatabaseName("ix_notification_request_id");
        builder.HasIndex(x => x.ResendOfId).HasDatabaseName("ix_notification_resend_of_id");
        builder.HasIndex(x => x.FallbackFromId).HasDatabaseName("ix_notification_fallback_from_id");

        builder.Property(x => x.Tags).IsRequired(false);
        builder.Property(x => x.Language).IsRequired(false);
        builder.Property(x => x.ServiceId).IsRequired(false);

        builder.Property<uint>("xmin").IsRowVersion();
        builder.Ignore(x => x.Events);
        builder.Ignore(x => x.Metadata);
    }
}
