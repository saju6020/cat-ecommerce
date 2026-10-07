using CatEcommerce.Infrastructure.Persistence.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

public sealed class CatalogueTermConfiguration : IEntityTypeConfiguration<CatalogueTermRow>
{
    public void Configure(EntityTypeBuilder<CatalogueTermRow> builder)
    {
        builder.HasNoKey();
        builder.ToView("catalogue_term", "product");

        builder.Property(x => x.Term).HasColumnName("term");
    }
}
