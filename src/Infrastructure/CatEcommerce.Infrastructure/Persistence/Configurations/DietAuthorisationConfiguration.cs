using CatEcommerce.Domain.Aggregates.Prescription;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>
/// HO-PHM-001 §8.10 / §12.1 (ADR-149) -- <c>pharmacy.diet_authorisation</c>. At most one ACTIVE authorisation per cat and product: <c>ix_diet_authorisation_active</c> is UNIQUE on
/// <c>(cat_id, product_id)</c> filtered to <c>Active</c> status -- the value written FROM the enumeration, never a literal (U-X3) -- the race guard behind the handler's own check.
/// Dates are branch-local <c>date</c>s, inclusive both ends. No foreign key to the cat, product or visit (ADR-011, ADR-151).
/// </summary>
public sealed class DietAuthorisationConfiguration : IEntityTypeConfiguration<DietAuthorisationAggregate>
{
    public void Configure(EntityTypeBuilder<DietAuthorisationAggregate> builder)
    {
        builder.ToTable("diet_authorisation", "pharmacy");
        builder.ConfigureAggregateBase();

        builder.Property(x => x.CatId).HasColumnName("cat_id").IsRequired();
        builder.Property(x => x.CustomerId).HasColumnName("customer_id").IsRequired();
        builder.Property(x => x.ProductId).HasColumnName("product_id").IsRequired();
        builder.Property(x => x.ProductNameSnapshot).HasColumnName("product_name_snapshot").HasMaxLength(200).IsRequired();
        builder.Property(x => x.VisitId).HasColumnName("visit_id").IsRequired();
        builder.Property(x => x.AuthorisedBy).HasColumnName("authorised_by").IsRequired();
        builder.Property(x => x.ValidFrom).HasColumnName("valid_from").HasColumnType("date").IsRequired();
        builder.Property(x => x.ValidTo).HasColumnName("valid_to").HasColumnType("date").IsRequired();
        builder.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(DietRules.MaxNotesLength);
        builder.Property(x => x.Status).HasColumnName("status").HasEnumerationValue().IsRequired();
        builder.Property(x => x.ExpiredOn).HasColumnName("expired_on").HasColumnType("date");
        builder.Property(x => x.RevokedOn).HasColumnName("revoked_on").HasColumnType("timestamptz");
        builder.Property(x => x.RevokedBy).HasColumnName("revoked_by");
        builder.Property(x => x.RevocationReason).HasColumnName("revocation_reason").HasMaxLength(DietRules.MaxReasonLength);
        builder.Property(x => x.ExtensionCount).HasColumnName("extension_count").IsRequired();

        builder.HasIndex(x => new { x.CatId, x.ProductId }).IsUnique().HasFilter($"status = {DietAuthorisationStatus.Active.Value}").HasDatabaseName("ix_diet_authorisation_active");
        builder.HasIndex(x => new { x.Status, x.ValidTo }).HasDatabaseName("ix_diet_authorisation_status_valid_to");
        builder.HasIndex(x => x.CustomerId).HasDatabaseName("ix_diet_authorisation_customer");
        builder.HasIndex(x => x.VisitId).HasDatabaseName("ix_diet_authorisation_visit");
    }
}
