using CatEcommerce.Domain.Aggregates.StoredFile;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>HO-FIL-001 §11.5: <c>files.stored_file</c>, indexes on <c>(purpose, uploaded_on)</c>
/// (the purge) and <c>(sha256)</c> (the duplicate report, OQ-6 -- not acted on, just reported).</summary>
public sealed class StoredFileConfiguration : IEntityTypeConfiguration<StoredFileAggregate>
{
    public void Configure(EntityTypeBuilder<StoredFileAggregate> builder)
    {
        builder.ToTable("stored_file", "files");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Purpose)
            .HasConversion(v => v.Value, v => Enumeration.FromValue<FilePurpose>(v))
            .IsRequired();

        builder.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.SizeInBytes).IsRequired();
        builder.Property(x => x.Sha256).HasMaxLength(64).IsRequired();
        builder.Property(x => x.OriginalFileName).HasMaxLength(255).IsRequired();
        builder.Property(x => x.StorageKey).HasMaxLength(500).IsRequired();
        builder.Property(x => x.UploadedBy).IsRequired();
        builder.Property(x => x.UploadedOn).HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.ScannerName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.RetainUntil).HasColumnType("date");
        builder.Property(x => x.LegalHold).IsRequired();

        builder.HasIndex(x => new { x.Purpose, x.UploadedOn });
        builder.HasIndex(x => x.Sha256);
        builder.HasIndex(x => x.UploadedBy);

        builder.Property(x => x.Tags).IsRequired(false);
        builder.Property(x => x.Language).IsRequired(false);
        builder.Property(x => x.ServiceId).IsRequired(false);

        builder.Property<uint>("xmin").IsRowVersion();
        builder.Ignore(x => x.Events);
        builder.Ignore(x => x.Metadata);
    }
}
