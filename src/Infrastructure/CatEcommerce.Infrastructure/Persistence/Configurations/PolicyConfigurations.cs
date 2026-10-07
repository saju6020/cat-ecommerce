using CatEcommerce.Domain.Aggregates.Administration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

public sealed class PolicyDocumentConfiguration : IEntityTypeConfiguration<PolicyDocumentAggregate>
{
    public void Configure(EntityTypeBuilder<PolicyDocumentAggregate> builder)
    {
        builder.ToTable("policy_document", "admin");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Key).HasColumnName("key").HasMaxLength(80).IsRequired();
        builder.HasIndex(x => x.Key).IsUnique().HasDatabaseName("ix_policy_document_key");
        builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
        builder.Property(x => x.RequiresAcceptance).HasColumnName("requires_acceptance").IsRequired();
        builder.Property(x => x.CurrentVersionNumber).HasColumnName("current_version_number");

        builder.Property(x => x.Tags).IsRequired(false);
        builder.Property(x => x.Language).IsRequired(false);
        builder.Property(x => x.ServiceId).IsRequired(false);
        builder.Property<uint>("xmin").IsRowVersion();
        builder.Ignore(x => x.Events);
        builder.Ignore(x => x.Metadata);
    }
}

public sealed class PolicyVersionConfiguration : IEntityTypeConfiguration<PolicyVersionAggregate>
{
    public void Configure(EntityTypeBuilder<PolicyVersionAggregate> builder)
    {
        builder.ToTable("policy_version", "admin");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.PolicyKey).HasColumnName("policy_key").HasMaxLength(80).IsRequired();
        builder.Property(x => x.VersionNumber).HasColumnName("version_number").IsRequired();
        builder.Property(x => x.Body).HasColumnName("body").HasMaxLength(50_000).IsRequired();
        builder.Property(x => x.PublishedOn).HasColumnName("published_on").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.ChangeSummary).HasColumnName("change_summary").HasMaxLength(500).IsRequired();
        builder.Property(x => x.RequiresAcceptance).HasColumnName("requires_acceptance").IsRequired();

        builder.HasIndex(x => new { x.PolicyKey, x.VersionNumber }).IsUnique().HasDatabaseName("ix_policy_version_key_number");

        builder.Property(x => x.Tags).IsRequired(false);
        builder.Property(x => x.Language).IsRequired(false);
        builder.Property(x => x.ServiceId).IsRequired(false);
        builder.Property<uint>("xmin").IsRowVersion();
        builder.Ignore(x => x.Events);
        builder.Ignore(x => x.Metadata);
    }
}

public sealed class PolicyAcceptanceConfiguration : IEntityTypeConfiguration<PolicyAcceptanceAggregate>
{
    public void Configure(EntityTypeBuilder<PolicyAcceptanceAggregate> builder)
    {
        builder.ToTable("policy_acceptance", "admin");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.CustomerAccountId).HasColumnName("customer_account_id").IsRequired();
        builder.Property(x => x.PolicyKey).HasColumnName("policy_key").HasMaxLength(80).IsRequired();
        builder.Property(x => x.VersionNumber).HasColumnName("version_number").IsRequired();
        builder.Property(x => x.AcceptedOn).HasColumnName("accepted_on").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.IpAddress).HasColumnName("ip_address").HasMaxLength(64);

        builder.HasIndex(x => new { x.CustomerAccountId, x.PolicyKey, x.VersionNumber }).IsUnique().HasDatabaseName("ix_policy_acceptance_customer_key_number");

        builder.Property(x => x.Tags).IsRequired(false);
        builder.Property(x => x.Language).IsRequired(false);
        builder.Property(x => x.ServiceId).IsRequired(false);
        builder.Property<uint>("xmin").IsRowVersion();
        builder.Ignore(x => x.Events);
        builder.Ignore(x => x.Metadata);
    }
}
