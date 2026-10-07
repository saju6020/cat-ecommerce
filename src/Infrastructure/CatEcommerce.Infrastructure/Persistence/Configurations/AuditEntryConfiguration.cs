using CatEcommerce.Domain.Aggregates.Audit;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>
/// Append-only leaf, "audit" schema -- HO-CAT-001 section 12.1. Extended by HO-ACC-001 §12.1
/// (US-ACC-12): <c>ip_address</c>, <c>category</c>, <c>correlation_id</c> and the two indexes
/// investigation joins need. The immutability triggers on this table are added by the
/// <c>AddAccounts</c> migration (§11.9), not here -- EF Core has no trigger API. That migration
/// also backfills any audit row written before this column set existed (Cat Profiles' own rows)
/// to a valid <see cref="AuditCategory"/> before installing those triggers, since afterwards no
/// UPDATE is possible even for that one-time fix.
/// </summary>
public sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntryAggregate>
{
    public void Configure(EntityTypeBuilder<AuditEntryAggregate> builder)
    {
        builder.ToTable("audit_entry", "audit");
        builder.HasKey(x => x.Id);
        // See CatConfiguration for why every Id needs ValueGeneratedNever().
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.EntityId).IsRequired();
        builder.Property(x => x.Action).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.ActorUserId).IsRequired();
        builder.Property(x => x.OccurredOn).HasColumnType("timestamptz").IsRequired();
        // Integrator-authorised migration (HO-CAT-001 §1 Amended, 2026-09-26; SECURITY gate MEDIUM):
        // widened from character varying(2000) to text. AuditChangeCaptureInterceptor snapshots the
        // whole aggregate root row on every write, and the cat validators alone allow ~3,450
        // characters of root text (name, breed, colour, handling note, deceased note, dietary and
        // behavioural notes) -- comfortably over the old 2000-char cap, which failed the write with a
        // 500 once the snapshot exceeded it. Unbounded text is deliberate: this is a system-computed
        // snapshot, not user input, so there is no length invariant to enforce (see
        // AuditEntryAggregate.Record, which validates EntityType/Action/Reason but never
        // BeforeValue/AfterValue).
        builder.Property(x => x.BeforeValue).HasColumnType("text");
        builder.Property(x => x.AfterValue).HasColumnType("text");

        builder.Property(x => x.IpAddress).HasColumnName("ip_address").HasColumnType("inet");
        builder.Property(x => x.Category)
            .HasColumnName("category")
            .HasConversion(v => v.Value, v => Enumeration.FromValue<AuditCategory>(v))
            .IsRequired();
        builder.Property(x => x.CorrelationId).HasColumnName("correlation_id");
        builder.Property(x => x.IsRecorded).HasColumnName("is_recorded").IsRequired();

        builder.HasIndex(x => new { x.EntityType, x.EntityId, x.OccurredOn }); // FR-ADM-6, later
        builder.HasIndex(x => new { x.EntityType, x.EntityId });
        builder.HasIndex(x => x.CorrelationId);

        // HO-ADM-001 §12.1 -- US-ADM-06's search filters. The only change to audit.* this
        // handover makes; the triggers and every other column stay HO-ACC-001's.
        builder.HasIndex(x => new { x.ActorUserId, x.OccurredOn }).HasDatabaseName("ix_audit_entry_actor_occurred");
        builder.HasIndex(x => new { x.EntityType, x.OccurredOn }).HasDatabaseName("ix_audit_entry_entity_type_occurred");
        builder.HasIndex(x => new { x.Action, x.OccurredOn }).HasDatabaseName("ix_audit_entry_action_occurred");
        builder.HasIndex(x => x.OccurredOn).HasDatabaseName("ix_audit_entry_occurred");

        // See CatConfiguration for why Tags/Language/ServiceId must be optional.
        builder.Property(x => x.Tags).IsRequired(false);
        builder.Property(x => x.Language).IsRequired(false);
        builder.Property(x => x.ServiceId).IsRequired(false);

        builder.Property<uint>("xmin").IsRowVersion();
        builder.Ignore(x => x.Events);
        builder.Ignore(x => x.Metadata);
    }
}
