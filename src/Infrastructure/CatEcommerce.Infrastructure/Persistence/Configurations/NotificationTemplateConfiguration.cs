using CatEcommerce.Domain.Aggregates.Notification;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>HO-NTF-001 §12.1.</summary>
public sealed class NotificationTemplateConfiguration : IEntityTypeConfiguration<NotificationTemplateAggregate>
{
    public void Configure(EntityTypeBuilder<NotificationTemplateAggregate> builder)
    {
        builder.ToTable("notification_template", "notify");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Key).HasColumnName("key").HasMaxLength(80).IsRequired();
        builder.HasIndex(x => x.Key).IsUnique().HasDatabaseName("ix_notification_template_key");

        builder.Property(x => x.Locale).HasColumnName("locale").HasMaxLength(10).IsRequired();
        builder.Property(x => x.ContentVersion).HasColumnName("content_version").IsRequired();
        builder.Property(x => x.LastEditedBy).HasColumnName("last_edited_by").IsRequired();
        builder.Property(x => x.LastEditedOn).HasColumnName("last_edited_on").HasColumnType("timestamptz").IsRequired();

        builder.OwnsMany(x => x.Contents, content =>
        {
            content.ToTable("notification_template_content", "notify");
            content.WithOwner().HasForeignKey("template_id");
            content.HasKey("Id");
            content.Property(c => c.Id).ValueGeneratedNever();
            content.Property(c => c.Channel).HasColumnName("channel")
                .HasConversion(v => v.Value, v => Enumeration.FromValue<NotificationChannel>(v)).IsRequired();
            content.Property(c => c.Subject).HasColumnName("subject").HasMaxLength(150);
            content.Property(c => c.Title).HasColumnName("title").HasMaxLength(100);
            content.Property(c => c.Body).HasColumnName("body").HasMaxLength(10_000).IsRequired();
            content.Property(c => c.LinkPath).HasColumnName("link_path").HasMaxLength(500);
            content.HasIndex("template_id", "Channel").IsUnique().HasDatabaseName("ix_notification_template_content_template_channel");
        });
        builder.Navigation(x => x.Contents).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(x => x.Tags).IsRequired(false);
        builder.Property(x => x.Language).IsRequired(false);
        builder.Property(x => x.ServiceId).IsRequired(false);

        builder.Property<uint>("xmin").IsRowVersion();
        builder.Ignore(x => x.Events);
        builder.Ignore(x => x.Metadata);
    }
}
