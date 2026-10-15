using System.Reflection;
using System.Text.RegularExpressions;
using CatEcommerce.Domain.Aggregates.Prescription;
using CatEcommerce.Domain.Events.Prescription;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.Tests.TestSupport;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Tests.Common;

/// <summary>HO-PHM-001 §15 groups A (U-A3), D (the register) and X -- the cross-cutting rules that fail the build rather than rely on review.</summary>
public sealed class PharmacyCrossCuttingTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 9, 8, 0, 0, TimeSpan.Zero);

    // ── U-A3 ────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(NotificationTemplateCatalogue.PharmacyRefillDue)]
    [InlineData(NotificationTemplateCatalogue.PharmacyRepeatReady)]
    [InlineData(NotificationTemplateCatalogue.PharmacyRepeatDeclined)]
    [InlineData(NotificationTemplateCatalogue.PharmacyDietAuthorised)]
    [InlineData(NotificationTemplateCatalogue.PharmacyDietAuthorisationExpired)]
    public void U_A3_EveryPharmacyTemplate_PassesTemplateRules_IsTransactional_AndNormal(string key)
    {
        var definition = NotificationTemplateCatalogue.Find(key);

        Assert.NotNull(definition);
        Assert.Equal(NotificationCategory.Transactional, definition!.Category);
        Assert.Equal(NotificationUrgency.Normal, definition.Urgency);

        var contents = definition.Defaults.Select(d => new TemplateContentInput(d.Channel, d.Subject, d.Title, d.Body, d.LinkPath)).ToList();
        Assert.Null(TemplateRules.Validate(definition, contents));
    }

    [Fact]
    public void U_A3b_NoPharmacyTemplate_CarriesADoseAClinicalNoteOrADiagnosisVariable()
    {
        var names = new[]
        {
            NotificationTemplateCatalogue.PharmacyRefillDue, NotificationTemplateCatalogue.PharmacyRepeatReady, NotificationTemplateCatalogue.PharmacyRepeatDeclined,
            NotificationTemplateCatalogue.PharmacyDietAuthorised, NotificationTemplateCatalogue.PharmacyDietAuthorisationExpired
        }.SelectMany(k => NotificationTemplateCatalogue.Find(k)!.Variables.Select(v => v.Name)).ToList();

        Assert.DoesNotContain(names, n => Regex.IsMatch(n, "dose|diagnos|instruction|allerg|note|weight", RegexOptions.IgnoreCase));
    }

    // ── U-X1 ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void U_X1_NoPharmacyEventCarriesAStringProperty()
    {
        var eventTypes = typeof(PrescriptionIssuedEvent).Assembly.GetTypes()
            .Where(t => t.Namespace == typeof(PrescriptionIssuedEvent).Namespace && typeof(Event).IsAssignableFrom(t) && !t.IsAbstract)
            .ToList();

        Assert.NotEmpty(eventTypes);

        var violations = eventTypes
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(p => p.PropertyType == typeof(string))
            .Select(p => $"{p.DeclaringType!.Name}.{p.Name}")
            .ToList();

        Assert.True(violations.Count == 0, "Pharmacy events carry ids, instants, dates, enums, booleans and decimals only: " + string.Join(", ", violations));
    }

    [Fact]
    public void U_X1b_EveryEventTheHandoverListsExists()
    {
        string[] expected =
        [
            "FormularyMedicineCreatedEvent", "FormularyMedicineUpdatedEvent", "FormularyMedicineControlledStatusChangedEvent", "FormularyMedicineActivationChangedEvent",
            "PrescriptionIssuedEvent", "PrescriptionCancelledEvent", "PrescriptionExpiredEvent", "PrescriptionDispensedEvent", "RepeatRequestedEvent", "RepeatRequestDeclinedEvent",
            "DispensingReversedEvent", "LabelPrintedEvent", "ControlledDrugRegisterEntryRecordedEvent",
            "DietAuthorisationIssuedEvent", "DietAuthorisationExtendedEvent", "DietAuthorisationExpiredEvent", "DietAuthorisationRevokedEvent",
        ];

        var actual = typeof(PrescriptionIssuedEvent).Assembly.GetTypes()
            .Where(t => t.Namespace == typeof(PrescriptionIssuedEvent).Namespace && typeof(Event).IsAssignableFrom(t)).Select(t => t.Name).ToHashSet();

        var missing = expected.Where(e => !actual.Contains(e)).ToList();
        Assert.True(missing.Count == 0, "Missing events: " + string.Join(", ", missing));
    }

    // ── U-D1 / U-D2 / U-D7: the register entry ──────────────────────────────────────────────────

    private static ControlledDrugRegisterEntryDto Dto(
        ControlledDrugEntryKind kind, decimal delta, Guid? actor = null, Guid? witness = null, Guid? catId = null, Guid? customerId = null,
        Guid? corrects = null, string? reason = null, DateTimeOffset? occurred = null) => new(
        Guid.NewGuid(), Guid.NewGuid(), "Ketamine", "100 mg/ml", MedicineForm.Injection, "CD Schedule 2", DispensingUnit.Millilitre, null, null, "LOT", null,
        kind, delta, null, null, catId, customerId, actor ?? Guid.NewGuid(), witness, ControlledDrugSourceType.GoodsReceipt, Guid.NewGuid(), corrects, reason, null,
        occurred ?? Now, null);

    private static RegisterStamp Stamp() => new(Now, new PharmacyActor(TestUserContext.Create(), Guid.NewGuid()));

    private static DomainRuleViolationException Violation(Action action) => Assert.Throws<DomainRuleViolationException>(action);

    [Fact]
    public void U_D1_ADispensedEntry_NeedsAWitness_WhoIsNotTheActor_AndANegativeQuantity()
    {
        var actor = Guid.NewGuid();
        var witness = Guid.NewGuid();

        Assert.Equal(BusinessViolationCodes.WitnessRequired,
            Violation(() => ControlledDrugRegisterEntryAggregate.Record(Dto(ControlledDrugEntryKind.Dispensed, -1m, actor, null, Guid.NewGuid(), Guid.NewGuid()), Stamp())).ErrorCode);
        Assert.Equal(BusinessViolationCodes.WitnessMustDifferFromDispenser,
            Violation(() => ControlledDrugRegisterEntryAggregate.Record(Dto(ControlledDrugEntryKind.Dispensed, -1m, actor, actor, Guid.NewGuid(), Guid.NewGuid()), Stamp())).ErrorCode);
        Assert.Equal(BusinessViolationCodes.InvalidRegisterQuantity,
            Violation(() => ControlledDrugRegisterEntryAggregate.Record(Dto(ControlledDrugEntryKind.Dispensed, 1m, actor, witness, Guid.NewGuid(), Guid.NewGuid()), Stamp())).ErrorCode);

        var ok = ControlledDrugRegisterEntryAggregate.Record(Dto(ControlledDrugEntryKind.Dispensed, -1m, actor, witness, Guid.NewGuid(), Guid.NewGuid()), Stamp());
        Assert.Equal(-1m, ok.QuantityDelta);
        Assert.Equal(Now, ok.RecordedOn);
    }

    [Fact]
    public void U_D2_AReceipt_NeedsNoWitness() =>
        Assert.Equal(5m, ControlledDrugRegisterEntryAggregate.Record(Dto(ControlledDrugEntryKind.Receipt, 5m), Stamp()).QuantityDelta);

    [Fact]
    public void U_D7_ACorrection_NamesItsTarget_AndGivesAReason()
    {
        var actor = Guid.NewGuid();
        var witness = Guid.NewGuid();

        Assert.Equal(BusinessViolationCodes.CorrectionTargetMismatch,
            Violation(() => ControlledDrugRegisterEntryAggregate.Record(Dto(ControlledDrugEntryKind.Correction, 0m, actor, witness, reason: "Miscounted"), Stamp())).ErrorCode);
        Assert.Equal(BusinessViolationCodes.CorrectionReasonRequired,
            Violation(() => ControlledDrugRegisterEntryAggregate.Record(Dto(ControlledDrugEntryKind.Correction, 0m, actor, witness, corrects: Guid.NewGuid()), Stamp())).ErrorCode);

        var ok = ControlledDrugRegisterEntryAggregate.Record(Dto(ControlledDrugEntryKind.Correction, -1m, actor, witness, corrects: Guid.NewGuid(), reason: "Miscounted"), Stamp());
        Assert.NotNull(ok.CorrectsEntryId);
    }

    [Fact]
    public void U_D1b_AnEntryOlderThanADay_OrInTheFuture_IsRefused()
    {
        Assert.Equal(BusinessViolationCodes.InvalidRegisterEntry,
            Violation(() => ControlledDrugRegisterEntryAggregate.Record(Dto(ControlledDrugEntryKind.Receipt, 5m, occurred: Now.AddHours(-25)), Stamp())).ErrorCode);
        Assert.Equal(BusinessViolationCodes.InvalidRegisterEntry,
            Violation(() => ControlledDrugRegisterEntryAggregate.Record(Dto(ControlledDrugEntryKind.Receipt, 5m, occurred: Now.AddMinutes(5)), Stamp())).ErrorCode);
    }

    // ── U-D3 ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void U_D3_TheRegisterEntryHasOnlyAFactory_AndNoPublicSetter()
    {
        foreach (var type in new[] { typeof(ControlledDrugRegisterEntryAggregate), typeof(ControlledDrugRegisterSealAggregate) })
        {
            var declared = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName && !m.IsPrivate)
                .Select(m => m.Name).ToList();

            // The only non-private methods are the static factories that create a new, complete record.
            Assert.All(type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Where(m => !m.IsSpecialName && !m.IsPrivate),
                m => Assert.True(m.IsStatic && m.ReturnType == type, $"{type.Name}.{m.Name} is not a factory."));
            Assert.NotEmpty(declared);

            var setters = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(p => p.SetMethod is { IsPublic: true } || p.SetMethod is { IsAssembly: true } || p.SetMethod is { IsFamilyOrAssembly: true }).ToList();
            Assert.Empty(setters);
        }
    }

    // ── U-D5 / U-D6 ─────────────────────────────────────────────────────────────────────────────

    private static ControlledDrugCanonicalFields Fields(long number, decimal delta = -1m, string notes = "") => new(
        Guid.Parse("00000000-0000-0000-0000-00000000000" + number), number, Guid.Empty, Guid.Empty, "Ketamine", "100 mg/ml", "Injection", "CD Schedule 2", "Millilitre", null, null,
        "LOT", null, "Dispensed", delta, null, null, null, null, Guid.Empty, null, "Dispensing", Guid.Empty, null, null, null, Now, Now, notes);

    [Fact]
    public void U_D5_AlteringTheSecondEntryOfAThreeEntryChain_BreaksTheChainAtPositionTwo()
    {
        var entries = new[] { Fields(1), Fields(2), Fields(3) };
        var sealedHashes = new List<string>();
        var previous = ControlledDrugEntryCanonicalForm.GenesisHash;
        foreach (var entry in entries)
        {
            previous = ControlledDrugEntryCanonicalForm.Hash(previous, entry);
            sealedHashes.Add(previous);
        }

        // Recompute from the (tampered) entries and compare with what was sealed.
        var tampered = new[] { Fields(1), Fields(2, delta: -2m), Fields(3) };
        long? firstBroken = null;
        previous = ControlledDrugEntryCanonicalForm.GenesisHash;
        for (var i = 0; i < tampered.Length; i++)
        {
            previous = ControlledDrugEntryCanonicalForm.Hash(previous, tampered[i]);
            if (previous != sealedHashes[i]) { firstBroken = i + 1; break; }
        }

        Assert.Equal(2, firstBroken);
        Assert.Equal(64, sealedHashes[0].Length);
    }

    [Fact]
    public void U_D5b_TheCanonicalForm_IsDeterministic_AndTruncatesInstantsToMicroseconds()
    {
        var a = Fields(1);
        var b = a with { OccurredOn = a.OccurredOn.AddTicks(7), RecordedOn = a.RecordedOn.AddTicks(3) }; // sub-microsecond: lost by Postgres, so must not change the hash

        Assert.Equal(ControlledDrugEntryCanonicalForm.Build(a), ControlledDrugEntryCanonicalForm.Build(b));
        Assert.Equal(ControlledDrugEntryCanonicalForm.Build(a), ControlledDrugEntryCanonicalForm.Build(Fields(1)));
        Assert.NotEqual(ControlledDrugEntryCanonicalForm.Build(a), ControlledDrugEntryCanonicalForm.Build(a with { Notes = "x" }));
    }

    [Fact]
    public void U_D6_TheCanonicalFieldList_EqualsTheEntrysPersistedProperties()
    {
        // Every persisted property of the entry (its own, plus the aggregate's Id) -- audit/housekeeping columns the base class adds are not part of the legal record.
        var entryProperties = typeof(ControlledDrugRegisterEntryAggregate)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Select(p => p.Name)
            .Prepend("Id").ToHashSet();

        var canonical = ControlledDrugEntryCanonicalForm.FieldNames.ToHashSet();

        Assert.True(entryProperties.SetEquals(canonical),
            "Entry properties not in the canonical form: " + string.Join(", ", entryProperties.Except(canonical)) +
            "; canonical fields the entry does not have: " + string.Join(", ", canonical.Except(entryProperties)));
    }

    // ── U-X3 / U-X6: source scans ───────────────────────────────────────────────────────────────

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CatEcommerce.sln")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new InvalidOperationException("CatEcommerce.sln was not found above the test binaries.");
    }

    [Fact]
    public void U_X3_ThePharmacyFilteredIndexes_AreWrittenFromTheEnumerationValue_NotALiteral()
    {
        var configurations = Path.Combine(RepositoryRoot(), "src", "Infrastructure", "CatEcommerce.Infrastructure", "Persistence", "Configurations");
        var offenders = new List<string>();
        var filters = 0;

        foreach (var file in new[] { "DietAuthorisationConfiguration.cs", "DispensingConfiguration.cs", "PrescriptionConfiguration.cs", "FormularyMedicineConfiguration.cs", "ControlledDrugRegisterConfigurations.cs" })
        {
            foreach (var match in Regex.Matches(File.ReadAllText(Path.Combine(configurations, file)), @"HasFilter\((?<arg>[^;]*?)\)\.HasDatabaseName|HasFilter\((?<arg2>[^;]*?)\);"))
            {
                filters++;
                var argument = ((Match)match).Value;
                if (Regex.IsMatch(argument, @"HasFilter\(\s*""[^""]*\b(status|kind)\b\s*=\s*\d")) offenders.Add($"{file}: {argument}");
            }
        }

        Assert.True(filters > 0, "No pharmacy filtered index was found - the scan is out of date.");
        Assert.True(offenders.Count == 0, "A filtered index compares an enumeration column with a literal number: " + string.Join("; ", offenders));
    }

    [Fact]
    public void U_X3b_TheMigrationPredicates_AreInterpolatedFromTheEnumeration_NotTyped()
    {
        var migrations = Path.Combine(RepositoryRoot(), "src", "Infrastructure", "CatEcommerce.Infrastructure", "Persistence", "Migrations");

        string FilterOf(string migrationSuffix, string indexName)
        {
            var file = Directory.EnumerateFiles(migrations, $"*{migrationSuffix}.cs").Single(f => !f.EndsWith(".Designer.cs"));
            var text = File.ReadAllText(file);
            var index = text.IndexOf($"\"{indexName}\"", StringComparison.Ordinal);
            Assert.True(index >= 0, $"{indexName} not found in {migrationSuffix}");
            return Regex.Match(text[index..], @"filter:\s*(?<f>\$?""[^""]+"")").Groups["f"].Value;
        }

        Assert.Equal("$\"status = {DietAuthorisationStatus.Active.Value}\"", FilterOf("AddDietAuthorisations", "ix_diet_authorisation_active"));
        Assert.Equal("$\"status = {DispensingStatus.Dispensed.Value}\"", FilterOf("AddPharmacyDispensing", "ix_dispensing_supply"));
    }

    [Fact]
    public void U_X6_ThePharmacyReadSide_NeverRehydratesAnAggregate()
    {
        var directory = Path.Combine(RepositoryRoot(), "src", "Read", "CatEcommerce.Read", "QueryHandlers", "Prescription");
        var wholeEntity = new Regex(@"AsNoTracking\(\)\s*\.\s*(FirstOrDefaultAsync|SingleOrDefaultAsync|SingleAsync|FirstAsync)\s*\(");
        var dictionary = new Regex(@"\.ToDictionaryAsync\s*\(");

        var files = Directory.EnumerateFiles(directory, "*.cs").ToList();
        Assert.NotEmpty(files);

        var offenders = files
            .Where(f => { var text = File.ReadAllText(f); return wholeEntity.IsMatch(text) || dictionary.IsMatch(text); })
            .Select(Path.GetFileName).ToList();

        Assert.True(offenders.Count == 0, "These Pharmacy read handlers rehydrate an aggregate instead of projecting: " + string.Join(", ", offenders));
    }
}
