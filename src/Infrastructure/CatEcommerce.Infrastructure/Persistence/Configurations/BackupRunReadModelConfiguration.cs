using CatEcommerce.Infrastructure.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>HO-XCT-001 section 8.7 -- <c>ops.backup_run</c>, a read model over a table only the backup job writes. No navigation; queried <c>AsNoTracking()</c>.</summary>
public sealed class BackupRunReadModelConfiguration : IEntityTypeConfiguration<BackupRunReadModel>
{
    public void Configure(EntityTypeBuilder<BackupRunReadModel> builder)
    {
        builder.ToTable("backup_run", "ops");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.Kind).HasColumnName("kind").HasColumnType("text").IsRequired();
        builder.Property(x => x.StartedOn).HasColumnName("started_on").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.FinishedOn).HasColumnName("finished_on").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.Succeeded).HasColumnName("succeeded").IsRequired();
        builder.Property(x => x.Repository).HasColumnName("repository").HasColumnType("text").IsRequired();
        builder.Property(x => x.BackupLabel).HasColumnName("backup_label").HasColumnType("text");
        builder.Property(x => x.SizeBytes).HasColumnName("size_bytes");
        builder.Property(x => x.WalArchivedUntil).HasColumnName("wal_archived_until").HasColumnType("timestamptz");
        builder.Property(x => x.ErrorSummary).HasColumnName("error_summary").HasMaxLength(BackupRunReadModel.MaxErrorSummaryLength);

        builder.HasIndex(x => new { x.Kind, x.FinishedOn }).HasDatabaseName("ix_backup_run_kind_finished_on");
    }
}
