using CatEcommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>HO-ORD-001 §12.1 -- <c>ops.idempotency_record</c>: unique <c>(scope, key)</c> is the claim; <c>(created_on)</c> serves the 24-hour purge.</summary>
public sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("idempotency_record", "ops");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(x => x.Scope).HasColumnName("scope").HasMaxLength(300).IsRequired();
        builder.Property(x => x.Key).HasColumnName("key").IsRequired();
        builder.Property(x => x.RequestHash).HasColumnName("request_hash").HasColumnType("char(64)").IsRequired();
        builder.Property(x => x.State).HasColumnName("state").HasConversion<int>().IsRequired();
        builder.Property(x => x.ResponseStatus).HasColumnName("response_status");
        builder.Property(x => x.ResponseContentType).HasColumnName("response_content_type").HasMaxLength(100);
        builder.Property(x => x.ResponseBody).HasColumnName("response_body");
        builder.Property(x => x.CreatedOn).HasColumnName("created_on").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.CompletedOn).HasColumnName("completed_on").HasColumnType("timestamptz");

        builder.HasIndex(x => new { x.Scope, x.Key }).IsUnique().HasDatabaseName("ix_idempotency_record_scope_key");
        builder.HasIndex(x => x.CreatedOn).HasDatabaseName("ix_idempotency_record_created_on");
    }
}

/// <summary>HO-ORD-001 §12.1 -- <c>ops.inbound_webhook_receipt</c>: unique <c>(provider, event_id)</c> makes a redelivered event a no-op.</summary>
public sealed class InboundWebhookReceiptConfiguration : IEntityTypeConfiguration<InboundWebhookReceipt>
{
    public void Configure(EntityTypeBuilder<InboundWebhookReceipt> builder)
    {
        builder.ToTable("inbound_webhook_receipt", "ops");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(x => x.Provider).HasColumnName("provider").HasMaxLength(50).IsRequired();
        builder.Property(x => x.EventId).HasColumnName("event_id").HasMaxLength(200).IsRequired();
        builder.Property(x => x.ReceivedOn).HasColumnName("received_on").HasColumnType("timestamptz").IsRequired();

        builder.HasIndex(x => new { x.Provider, x.EventId }).IsUnique().HasDatabaseName("ix_inbound_webhook_receipt_provider_event");
    }
}
