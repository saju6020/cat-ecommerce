using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>HO-ACC-001 §12.1. Append-only.</summary>
public sealed class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetTokenAggregate>
{
    public void Configure(EntityTypeBuilder<PasswordResetTokenAggregate> builder)
    {
        builder.ToTable("password_reset_token", "account");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.AccountKind)
            .HasColumnName("account_kind")
            .HasConversion(v => v.Value, v => Enumeration.FromValue<AccountKind>(v))
            .IsRequired();

        builder.Property(x => x.AccountId).HasColumnName("account_id").IsRequired();
        builder.Property(x => x.TokenHash).HasColumnName("token_hash").HasMaxLength(128).IsRequired();
        builder.Property(x => x.IssuedOn).HasColumnName("issued_on").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.ExpiresOn).HasColumnName("expires_on").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.IssuedForSecurityStamp).HasColumnName("issued_for_security_stamp").IsRequired();

        builder.Property(x => x.Channel)
            .HasColumnName("channel")
            .HasConversion(v => v.Value, v => Enumeration.FromValue<PasswordResetChannel>(v))
            .IsRequired();

        builder.HasIndex(x => x.TokenHash).IsUnique().HasDatabaseName("ix_password_reset_token_token_hash");

        builder.Property(x => x.Tags).IsRequired(false);
        builder.Property(x => x.Language).IsRequired(false);
        builder.Property(x => x.ServiceId).IsRequired(false);

        builder.Property<uint>("xmin").IsRowVersion();
        builder.Ignore(x => x.Events);
        builder.Ignore(x => x.Metadata);
    }
}
