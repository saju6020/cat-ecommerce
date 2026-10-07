using CatEcommerce.Domain.Aggregates.Administration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

public sealed class StaffBranchAssignmentConfiguration : IEntityTypeConfiguration<StaffBranchAssignmentAggregate>
{
    public void Configure(EntityTypeBuilder<StaffBranchAssignmentAggregate> builder)
    {
        builder.ToTable("staff_branch_assignment", "admin");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.PrimitiveCollection(x => x.BranchIds)
            .HasColumnName("branch_ids")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(x => x.PrimaryBranchId).HasColumnName("primary_branch_id").IsRequired();

        builder.Property(x => x.Tags).IsRequired(false);
        builder.Property(x => x.Language).IsRequired(false);
        builder.Property(x => x.ServiceId).IsRequired(false);
        builder.Property<uint>("xmin").IsRowVersion();
        builder.Ignore(x => x.Events);
        builder.Ignore(x => x.Metadata);
    }
}
