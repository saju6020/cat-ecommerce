using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>HO-ACC-001 §12.1.</summary>
public sealed class StaffSessionConfiguration : IEntityTypeConfiguration<StaffSessionAggregate>
{
    public void Configure(EntityTypeBuilder<StaffSessionAggregate> builder)
    {
        builder.ToTable("staff_session", "account");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.StaffAccountId).HasColumnName("staff_account_id").IsRequired();
        builder.Property(x => x.TokenHash).HasColumnName("token_hash").HasMaxLength(128).IsRequired();
        builder.Property(x => x.CsrfTokenHash).HasColumnName("csrf_token_hash").HasMaxLength(128).IsRequired();
        builder.Property(x => x.IssuedForSecurityStamp).HasColumnName("issued_for_security_stamp").IsRequired();
        builder.Property(x => x.IssuedOn).HasColumnName("issued_on").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.ExpiresOn).HasColumnName("expires_on").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.LastActivityOn).HasColumnName("last_activity_on").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.EndedOn).HasColumnName("ended_on").HasColumnType("timestamptz");
        builder.Property(x => x.IpAddress).HasColumnName("ip_address").HasMaxLength(45);
        builder.Property(x => x.UserAgent).HasColumnName("user_agent").HasMaxLength(512);

        builder.Property(x => x.EndReason)
            .HasColumnName("end_reason")
            .HasConversion(
                v => v == null ? (int?)null : v.Value,
                v => v == null ? null : Enumeration.FromValue<SessionEndReason>(v.Value));

        builder.HasIndex(x => x.TokenHash).IsUnique().HasDatabaseName("ix_staff_session_token_hash");
        builder.HasIndex(x => new { x.StaffAccountId, x.ExpiresOn }).HasDatabaseName("ix_staff_session_staff_account_id_expires_on");

        builder.Property(x => x.Tags).IsRequired(false);
        builder.Property(x => x.Language).IsRequired(false);
        builder.Property(x => x.ServiceId).IsRequired(false);

        builder.Property<uint>("xmin").IsRowVersion();
        builder.Ignore(x => x.Events);
        builder.Ignore(x => x.Metadata);
    }
}
