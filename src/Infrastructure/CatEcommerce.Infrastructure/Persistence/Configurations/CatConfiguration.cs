using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>HO-CAT-001 section 12.1.</summary>
public sealed class CatConfiguration : IEntityTypeConfiguration<CatAggregate>
{
    public void Configure(EntityTypeBuilder<CatAggregate> builder)
    {
        builder.ToTable("cat", "clinic");
        builder.HasKey(x => x.Id);
        // Every id in this system is minted by the application (controller/handler), never the
        // database -- .ai/persistence-ef-core.md section 8. EF Core 9 defaults a Guid key to
        // ValueGeneratedOnAdd; left at that default, a NEW owned-collection child added to an
        // already-tracked aggregate (e.g. AddPhoto) is misread by EF's Added-vs-Modified
        // heuristic as an existing row (non-default key + "the DB should have generated this"),
        // producing an UPDATE for a row that doesn't exist yet and a spurious
        // DbUpdateConcurrencyException on an ordinary first write -- found by running the host
        // against a real Postgres end to end. ValueGeneratedNever() on every Id fixes it.
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.OwnerId).IsRequired();
        builder.Property(x => x.OwnershipGeneration).HasColumnName("ownership_generation").HasDefaultValue(0).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Species).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Breed).HasMaxLength(100);
        builder.Property(x => x.ColourAndMarkings).HasMaxLength(200);
        builder.Property(x => x.DietaryNotes).HasMaxLength(1000);
        builder.Property(x => x.BehaviouralNotes).HasMaxLength(1000);
        builder.Property(x => x.HandlingNote).HasMaxLength(500);
        builder.Property(x => x.DeceasedNote).HasMaxLength(500);
        builder.Property(x => x.DateOfBirth).HasColumnType("date");
        builder.Property(x => x.NeuteredOn).HasColumnType("date");
        builder.Property(x => x.DeceasedOn).HasColumnType("date");

        builder.Property(x => x.Sex)
            .HasConversion(v => v.Value, v => Enumeration.FromValue<CatSex>(v))
            .IsRequired();

        builder.Property(x => x.DateOfBirthPrecision)
            .HasConversion(v => v.Value, v => Enumeration.FromValue<DateOfBirthPrecision>(v))
            .IsRequired();

        builder.Property(x => x.NeuterStatus)
            .HasConversion(v => v.Value, v => Enumeration.FromValue<NeuterStatus>(v))
            .IsRequired();

        builder.Property(x => x.LifeStatus)
            .HasConversion(v => v.Value, v => Enumeration.FromValue<CatLifeStatus>(v))
            .IsRequired();

        builder.HasIndex(x => x.OwnerId);
        builder.HasIndex(x => x.LifeStatus);

        ConfigurePhotos(builder);
        ConfigureAllergies(builder);
        ConfigureChronicConditions(builder);
        ConfigureMedications(builder);
        ConfigureVaccinations(builder);
        ConfigureParasiteTreatments(builder);

        // AggregateRoot -> Entity.Tags/Language/ServiceId are never set by this slice's
        // aggregates or by SetDefaultValue(UserContext) -- discovered by running the host end
        // to end (SaveChanges against the null CLR defaults throws, first for Tags being a
        // required primitive collection by EF Core 9 convention, then for Language/ServiceId
        // being required strings by convention). Mark all three optional rather than force
        // every aggregate to initialise fields this slice never uses.
        builder.Property(x => x.Tags).IsRequired(false);
        builder.Property(x => x.Language).IsRequired(false);
        builder.Property(x => x.ServiceId).IsRequired(false);

        builder.Property<uint>("xmin").IsRowVersion();
        builder.Ignore(x => x.Events);
        builder.Ignore(x => x.Metadata);
    }

    /// <summary>Gate-fix 2026-09-27 (ARCHITECTURE HIGH): stores <c>FileId</c> only -- content type,
    /// size and the original name are <c>StoredFileAggregate</c>'s own state.
    ///
    /// Gate-fix 2026-09-27, second pass (ARCHITECTURE HIGH): this unique index on <c>file_id</c> is
    /// now the ONLY enforcement of "one file can be attached to only one cat" -- there is no more
    /// state on <c>StoredFileAggregate</c> to check first (<c>AddCatPhotoCommandHandler</c> only
    /// asks <c>StoredFileAggregate.IsEligibleForAttach</c>, which is side-effect-free). Named
    /// explicitly (lowercase snake_case, matching every other unique index in this solution) so
    /// <c>UniqueConstraintExceptionFilter</c> can map its violation to the same
    /// <c>PhotoFileNotEligible</c> 400 the handler's own pre-check returns.</summary>
    private static void ConfigurePhotos(EntityTypeBuilder<CatAggregate> builder)
    {
        builder.OwnsMany(x => x.Photos, photo =>
        {
            photo.ToTable("cat_photo", "clinic");
            photo.WithOwner().HasForeignKey("cat_id");
            photo.HasKey("Id");
            photo.Property(p => p.Id).ValueGeneratedNever(); // see the root Id comment above
            photo.Property(p => p.FileId).IsRequired();
            photo.Property(p => p.UploadedOn).HasColumnType("timestamptz");
            photo.HasIndex(p => p.FileId).IsUnique().HasDatabaseName("ix_cat_photo_file_id");
        });
        builder.Navigation(x => x.Photos).UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    private static void ConfigureAllergies(EntityTypeBuilder<CatAggregate> builder)
    {
        builder.OwnsMany(x => x.Allergies, allergy =>
        {
            allergy.ToTable("cat_allergy", "clinic");
            allergy.WithOwner().HasForeignKey("cat_id");
            allergy.HasKey("Id");
            allergy.Property(a => a.Id).ValueGeneratedNever();
            allergy.Property(a => a.Allergen).HasMaxLength(200).IsRequired();
            allergy.Property(a => a.Reaction).HasMaxLength(500).IsRequired();
            allergy.Property(a => a.RecordedOn).HasColumnType("date");
            allergy.Property(a => a.Severity)
                .HasConversion(v => v.Value, v => Enumeration.FromValue<AllergySeverity>(v))
                .IsRequired();
        });
        builder.Navigation(x => x.Allergies).UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    private static void ConfigureChronicConditions(EntityTypeBuilder<CatAggregate> builder)
    {
        builder.OwnsMany(x => x.ChronicConditions, condition =>
        {
            condition.ToTable("cat_chronic_condition", "clinic");
            condition.WithOwner().HasForeignKey("cat_id");
            condition.HasKey("Id");
            condition.Property(c => c.Id).ValueGeneratedNever();
            condition.Property(c => c.Condition).HasMaxLength(200).IsRequired();
            condition.Property(c => c.Notes).HasMaxLength(1000);
            condition.Property(c => c.DiagnosedOn).HasColumnType("date");
        });
        builder.Navigation(x => x.ChronicConditions).UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    private static void ConfigureMedications(EntityTypeBuilder<CatAggregate> builder)
    {
        builder.OwnsMany(x => x.Medications, medication =>
        {
            medication.ToTable("cat_medication", "clinic");
            medication.WithOwner().HasForeignKey("cat_id");
            medication.HasKey("Id");
            medication.Property(m => m.Id).ValueGeneratedNever();
            medication.Property(m => m.MedicationName).HasMaxLength(200).IsRequired();
            medication.Property(m => m.Dosage).HasMaxLength(100).IsRequired();
            medication.Property(m => m.Frequency).HasMaxLength(100).IsRequired();
            medication.Property(m => m.StartedOn).HasColumnType("date");
            medication.Property(m => m.EndsOn).HasColumnType("date");
        });
        builder.Navigation(x => x.Medications).UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    private static void ConfigureVaccinations(EntityTypeBuilder<CatAggregate> builder)
    {
        builder.OwnsMany(x => x.Vaccinations, vaccination =>
        {
            vaccination.ToTable("cat_vaccination", "clinic");
            vaccination.WithOwner().HasForeignKey("cat_id");
            vaccination.HasKey("Id");
            vaccination.Property(v => v.Id).ValueGeneratedNever();
            vaccination.Property(v => v.VaccineCode).HasMaxLength(50).IsRequired();
            vaccination.Property(v => v.VaccineName).HasMaxLength(200).IsRequired();
            vaccination.Property(v => v.BatchReference).HasMaxLength(100).IsRequired();
            vaccination.Property(v => v.Notes).HasMaxLength(500);
            vaccination.Property(v => v.BatchExpiresOn).HasColumnType("date");
            vaccination.Property(v => v.AdministeredOn).HasColumnType("date");
            vaccination.Property(v => v.NextDueOn).HasColumnType("date");
            vaccination.HasIndex(v => v.NextDueOn); // drives US-CAT-07's due/overdue report
        });
        builder.Navigation(x => x.Vaccinations).UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    private static void ConfigureParasiteTreatments(EntityTypeBuilder<CatAggregate> builder)
    {
        builder.OwnsMany(x => x.ParasiteTreatments, treatment =>
        {
            treatment.ToTable("cat_parasite_treatment", "clinic");
            treatment.WithOwner().HasForeignKey("cat_id");
            treatment.HasKey("Id");
            treatment.Property(t => t.Id).ValueGeneratedNever();
            treatment.Property(t => t.ProductName).HasMaxLength(200).IsRequired();
            treatment.Property(t => t.BatchReference).HasMaxLength(100);
            treatment.Property(t => t.Notes).HasMaxLength(500);
            treatment.Property(t => t.AdministeredOn).HasColumnType("date");
            treatment.Property(t => t.NextDueOn).HasColumnType("date");
            treatment.Property(t => t.TreatmentType)
                .HasConversion(v => v.Value, v => Enumeration.FromValue<ParasiteTreatmentType>(v))
                .IsRequired();
        });
        builder.Navigation(x => x.ParasiteTreatments).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
