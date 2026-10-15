using System.Reflection;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Events.Visit;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Tests.Common;

/// <summary>HO-EMR-001 §15 -- U-G3 (the two templates), U-X1 (no string on a visit event), U-X4
/// (authorisation table) and the cross-cutting duties C-ALL-1 / C-ALL-5.</summary>
public sealed class ConsultationCrossCuttingTests
{
    // ── U-G3 ────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(NotificationTemplateCatalogue.VisitSummaryAvailable, "summaryLink")]
    [InlineData(NotificationTemplateCatalogue.VisitFollowUpDue, "bookingLink")]
    public void U_G3_TheTwoDefinitions_PassTemplateRules_AreTransactional_AndCarryNoClinicalVariable(string key, string linkVariable)
    {
        var definition = NotificationTemplateCatalogue.Find(key);

        Assert.NotNull(definition);
        Assert.Equal(NotificationCategory.Transactional, definition!.Category);
        Assert.Equal(NotificationUrgency.Normal, definition.Urgency);

        var contents = definition.Defaults
            .Select(d => new TemplateContentInput(d.Channel, d.Subject, d.Title, d.Body, d.LinkPath)).ToList();
        Assert.Null(TemplateRules.Validate(definition, contents));

        // Only the cat's name, a date and a link may travel -- never a diagnosis, vet name, reason or note.
        Assert.Subset(new HashSet<string> { "catName", "visitDate", "dueOn", linkVariable }, definition.Variables.Select(v => v.Name).ToHashSet());

        var link = definition.Variables.Single(v => v.Name == linkVariable);
        Assert.Equal(definition.Channels.OrderBy(c => c.Value), link.RequiredOn.OrderBy(c => c.Value));
    }

    // ── U-X1 ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void U_X1_NoVisitEventCarriesAStringProperty()
    {
        var eventTypes = typeof(VisitOpenedEvent).Assembly.GetTypes()
            .Where(t => t.Namespace == typeof(VisitOpenedEvent).Namespace && typeof(Event).IsAssignableFrom(t) && !t.IsAbstract)
            .ToList();

        Assert.NotEmpty(eventTypes);

        // Event base members (CorrelationId, UserContext, ...) are the platform's; only members the
        // event itself declares count.
        var offenders = eventTypes
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(p => p.PropertyType == typeof(string))
                .Select(p => $"{t.Name}.{p.Name}"))
            .ToList();

        Assert.Empty(offenders);
    }

    // ── U-X4 ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void U_X4_VisitsFinalise_IsVetOnly_NoAdministrator()
    {
        Assert.True(ActionRoles.TryGetRoles(Actions.Visits.Finalise, out var roles));
        Assert.Equal([StaffRoles.Vet], roles);
    }

    [Fact]
    public void U_X4_NoClinicalWriteAction_IncludesReceptionOrAdministrator()
    {
        string[] writes =
        [
            Actions.Visits.Open, Actions.Visits.RecordVitals, Actions.Visits.WriteNote, Actions.Visits.Finalise, Actions.Visits.AddAddendum,
        ];

        foreach (var action in writes)
        {
            Assert.True(ActionRoles.TryGetRoles(action, out var roles), action);
            Assert.DoesNotContain(StaffRoles.Reception, roles);
            Assert.DoesNotContain(StaffRoles.Administrator, roles);
        }
    }

    [Fact]
    public void U_X4_AppendixD_ClinicalNotes_NurseWritesVitalsOnly_AdminAndNurseCanRead()
    {
        Assert.True(ActionRoles.TryGetRoles(Actions.Visits.RecordVitals, out var vitals));
        Assert.Contains(StaffRoles.Nurse, vitals);
        Assert.True(ActionRoles.TryGetRoles(Actions.Visits.WriteNote, out var note));
        Assert.DoesNotContain(StaffRoles.Nurse, note);
        Assert.True(ActionRoles.TryGetRoles(Actions.Visits.View, out var view));
        Assert.Contains(StaffRoles.Administrator, view);
        Assert.DoesNotContain(StaffRoles.Reception, view);
    }

    [Fact]
    public void ViewOwn_IsACustomerAction_NoStaffRoleHoldsIt()
    {
        Assert.Contains(Actions.Visits.ViewOwn, CustomerActions.Cat);
        Assert.True(ActionRoles.TryGetRoles(Actions.Visits.ViewOwn, out var roles));
        Assert.Empty(roles);
    }

    [Fact]
    public void ConsentActions_AreOpenToReception_ForSignedConsentForms()
    {
        Assert.True(ActionRoles.TryGetRoles(Actions.Consents.Record, out var roles));
        Assert.Contains(StaffRoles.Reception, roles);
        Assert.DoesNotContain(StaffRoles.Accountant, roles);
    }

    // ── C-ALL-1 / C-ALL-5 ───────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("visit", "Clinical")]
    [InlineData("visit_problem", "Clinical")]
    [InlineData("visit_vitals", "Clinical")]
    [InlineData("visit_photo", "Clinical")]
    [InlineData("visit_addendum", "Clinical")]
    [InlineData("consultation_draft", "Clinical")]
    [InlineData("diagnostic_order", "Clinical")]
    [InlineData("diagnostic_requested_analyte", "Clinical")]
    [InlineData("diagnostic_result_set", "Clinical")]
    [InlineData("diagnostic_result_value", "Clinical")]
    [InlineData("diagnostic_attachment", "Clinical")]
    [InlineData("consent_record", "Regulatory")]
    [InlineData("diagnosis_code", "Operational")]
    [InlineData("lab_analyte", "Operational")]
    [InlineData("note_template", "Operational")]
    public void EveryConsultationTable_IsClassified_AndNeverHardDeletable(string table, string expectedClass)
    {
        var row = RecordClassification.Find("clinic", table);

        Assert.NotNull(row);
        Assert.Equal(expectedClass, row!.RecordClass.DisplayName);
        Assert.Equal(HardDeletePolicy.Forbidden, row.HardDeletePolicy);
    }

    [Fact]
    public void EveryClinicalConsultationRoot_HasItsAuditEntityTypeRegistered()
    {
        string[] roots = ["VisitAggregate", "VisitAddendumAggregate", "DiagnosticOrderAggregate", "ConsentRecordAggregate"];
        foreach (var root in roots) Assert.True(ClinicalAuditEntityTypes.Contains(root), root);

        // The six access-log kinds carry the kind name as their entity type (ADR-129).
        foreach (var kind in new[] { ClinicalRecordKind.Visit, ClinicalRecordKind.ClinicalHistory, ClinicalRecordKind.DiagnosticOrder,
                     ClinicalRecordKind.DischargeSummary, ClinicalRecordKind.Consent, ClinicalRecordKind.VisitList })
            Assert.True(ClinicalAuditEntityTypes.Contains(kind.DisplayName), kind.DisplayName);
    }

    [Fact]
    public void SeededSystemCode_MatchesTheMigrationLiterals()
    {
        // 20261007074749_AddClinicalCatalogues inserts these literals (a seed is frozen history).
        Assert.Equal(new Guid("5e1d0a6c-7b0e-4d0e-9a54-0c3a7c1b5a01"), DiagnosisCodeSeed.RoutineNoAbnormality.Id);
        Assert.Equal("SYS-NAD", DiagnosisCodeSeed.RoutineNoAbnormality.Code);
        Assert.Equal("Routine visit — no abnormality detected", DiagnosisCodeSeed.RoutineNoAbnormality.Name);
    }

    [Fact]
    public void FollowUpLeadDays_IsAnIntegerSetting_DefaultThree_NoBranchOverride()
    {
        var definition = SettingDefinitions.Find(SettingKeys.ClinicalFollowUpReminderLeadDays)!;

        Assert.Equal("3", definition.Default);
        Assert.False(definition.BranchOverridable);
        Assert.Equal(SettingType.Integer, definition.Type);
    }
}
