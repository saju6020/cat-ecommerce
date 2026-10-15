using CatEcommerce.Domain.Aggregates.Operations;
using CatEcommerce.Domain.Entities;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>HO-XCT-001 section 8.3 / 12.1 -- <c>ops.erasure_request</c> and its owned <c>ops.erasure_scope</c>. The key IS the customer account id.</summary>
public sealed class ErasureRequestConfiguration : IEntityTypeConfiguration<ErasureRequestAggregate>
{
    public void Configure(EntityTypeBuilder<ErasureRequestAggregate> builder)
    {
        builder.ToTable("erasure_request", "ops");
        builder.HasKey(x => x.Id).HasName("pk_erasure_request");
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.RequestedOn).HasColumnName("requested_on").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.Source).HasColumnName("source")
            .HasConversion(v => v.Value, v => Enumeration.FromValue<ErasureRequestSource>(v)).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status")
            .HasConversion(v => v.Value, v => Enumeration.FromValue<ErasureRequestStatus>(v)).IsRequired();
        builder.Property(x => x.CompletedOn).HasColumnName("completed_on").HasColumnType("timestamptz");

        builder.OwnsMany(x => x.Scopes, scope =>
        {
            scope.ToTable("erasure_scope", "ops");
            scope.WithOwner().HasForeignKey("erasure_request_id");
            scope.HasKey(s => s.Id);
            scope.Property(s => s.Id).ValueGeneratedNever();
            scope.Property(s => s.ContextKey).HasColumnName("context_key").HasMaxLength(ErasureScope.MaxContextKeyLength).IsRequired();
            scope.Property(s => s.Status).HasColumnName("status")
                .HasConversion(v => v.Value, v => Enumeration.FromValue<ErasureScopeStatus>(v)).IsRequired();
            scope.Property(s => s.HeldUntil).HasColumnName("held_until").HasColumnType("date");
            scope.Property(s => s.HeldBasis).HasColumnName("held_basis").HasMaxLength(ErasureScope.MaxHeldBasisLength);
            scope.Property(s => s.Attempts).HasColumnName("attempts").IsRequired();
            scope.Property(s => s.LastAttemptOn).HasColumnName("last_attempt_on").HasColumnType("timestamptz");
            scope.Property(s => s.LastErrorCode).HasColumnName("last_error_code").HasMaxLength(ErasureScope.MaxErrorCodeLength);
            scope.Property(s => s.CompletedOn).HasColumnName("completed_on").HasColumnType("timestamptz");

            // Due scopes: Pending, Failing past its back-off, Held whose date has passed (section 12.1).
            scope.HasIndex(s => new { s.Status, s.HeldUntil }).HasDatabaseName("ix_erasure_scope_status_held_until");
            scope.HasIndex("erasure_request_id", nameof(ErasureScope.ContextKey)).IsUnique().HasDatabaseName("ix_erasure_scope_request_context");
        });
        builder.Navigation(x => x.Scopes).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(x => new { x.Status, x.RequestedOn }).HasDatabaseName("ix_erasure_request_status_requested_on");

        builder.Property(x => x.Tags).IsRequired(false);
        builder.Property(x => x.Language).IsRequired(false);
        builder.Property(x => x.ServiceId).IsRequired(false);

        builder.Property<uint>("xmin").IsRowVersion();
        builder.Ignore(x => x.Events);
        builder.Ignore(x => x.Metadata);
    }
}
