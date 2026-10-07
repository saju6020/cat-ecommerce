using CatEcommerce.Domain.Aggregates.Administration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

public sealed class ServicePriceConfiguration : IEntityTypeConfiguration<ServicePriceAggregate>
{
    public void Configure(EntityTypeBuilder<ServicePriceAggregate> builder)
    {
        builder.ToTable("service_price", "admin");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.ClinicServiceId).HasColumnName("service_id").IsRequired();
        builder.Property(x => x.EffectiveFrom).HasColumnName("effective_from").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.SetBy).HasColumnName("set_by").IsRequired();

        builder.OwnsOne(x => x.Amount, m =>
        {
            m.Property(p => p.Amount).HasColumnName("amount").HasColumnType("numeric(12,2)").IsRequired();
            m.Property(p => p.CurrencyCode).HasColumnName("currency_code").HasColumnType("char(3)").IsRequired();
        });
        builder.Navigation(x => x.Amount).IsRequired();

        builder.HasIndex(x => new { x.ClinicServiceId, x.EffectiveFrom }).IsUnique().HasDatabaseName("ix_service_price_service_effective");

        builder.Property(x => x.Tags).IsRequired(false);
        builder.Property(x => x.Language).IsRequired(false);
        // The platform's own Entity.ServiceId (unrelated to this aggregate's ClinicServiceId
        // above, §14/F15) -- easy to miss on this one aggregate given the name collision that
        // forced the ClinicServiceId rename in the first place.
        builder.Property(x => x.ServiceId).IsRequired(false);
        builder.Property<uint>("xmin").IsRowVersion();
        builder.Ignore(x => x.Events);
        builder.Ignore(x => x.Metadata);
    }
}
