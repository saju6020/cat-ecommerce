using System.Net;
using CatEcommerce.Application.CommandHandlers.Visit;
using CatEcommerce.Application.Commands.Visit;
using CatEcommerce.Application.Tests.TestSupport;
using CatEcommerce.Domain.Aggregates.Audit;
using CatEcommerce.Domain.Aggregates.Visit;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace CatEcommerce.Application.Tests.CommandHandlers.Visit;

/// <summary>HO-EMR-001 §15 Phase A -- U-A1, U-A2 (handler level) and U-A5.</summary>
public sealed class ClinicalCatalogueHandlerTests
{
    private static DiagnosisCodeAggregate SeedCode(FakeAggregateRootRepository<DiagnosisCodeAggregate> repository, string code, Guid? id = null)
    {
        var aggregate = new DiagnosisCodeAggregate(id ?? Guid.NewGuid());
        aggregate.Create(new DiagnosisCodeCreateDto(code, "Name", null, null, null, TestUserContext.Create(), Guid.NewGuid()));
        repository.Seed(aggregate);
        return aggregate;
    }

    [Fact]
    public async Task CreateDiagnosisCode_DuplicateCode_IsRefused_And_NothingIsSaved() // U-A1
    {
        var repository = new FakeAggregateRootRepository<DiagnosisCodeAggregate>();
        SeedCode(repository, "FLUTD");
        var handler = new CreateDiagnosisCodeCommandHandler(repository);

        var response = await handler.HandleAsync(new CreateDiagnosisCodeCommand
        {
            DiagnosisCodeId = Guid.NewGuid(), Code = "FLUTD", Name = "Another", UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.False(response.IsSuccess);
        Assert.Equal(BusinessViolationCodes.DiagnosisCodeAlreadyExists, response.ValidationResult!.Errors.Single().ErrorCode);
        Assert.Empty(repository.Saved);
    }

    [Theory]
    [InlineData("lower")]
    [InlineData("   ")]
    public async Task CreateDiagnosisCode_LowerCaseOrBlankCode_IsRefusedByTheAggregate(string code) // U-A1
    {
        var repository = new FakeAggregateRootRepository<DiagnosisCodeAggregate>();
        var handler = new CreateDiagnosisCodeCommandHandler(repository);

        var response = await handler.HandleAsync(new CreateDiagnosisCodeCommand
        {
            DiagnosisCodeId = Guid.NewGuid(), Code = code, Name = "Name", UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.False(response.IsSuccess);
        Assert.Equal(BusinessViolationCodes.InvalidDiagnosisCode, response.ValidationResult!.Errors.Single().ErrorCode);
        Assert.Empty(repository.Saved);
    }

    [Fact]
    public async Task SetActive_OnTheSystemCode_IsRefused() // U-A2
    {
        var repository = new FakeAggregateRootRepository<DiagnosisCodeAggregate>();
        var systemCode = SeedCode(repository, DiagnosisCodeSeed.RoutineNoAbnormality.Code, DiagnosisCodeSeed.RoutineNoAbnormality.Id);
        var handler = new SetDiagnosisCodeActiveCommandHandler(repository);

        var response = await handler.HandleAsync(new SetDiagnosisCodeActiveCommand
        {
            DiagnosisCodeId = systemCode.Id, IsActive = false, UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.False(response.IsSuccess);
        Assert.Equal(BusinessViolationCodes.SystemDiagnosisCodeReadOnly, response.ValidationResult!.Errors.Single().ErrorCode);
        Assert.True(systemCode.IsActive);
        Assert.Empty(repository.Updated);
    }

    [Fact]
    public async Task UpdateDiagnosisCode_Unknown_IsNotFound()
    {
        var handler = new UpdateDiagnosisCodeCommandHandler(new FakeAggregateRootRepository<DiagnosisCodeAggregate>());

        var response = await handler.HandleAsync(new UpdateDiagnosisCodeCommand
        {
            DiagnosisCodeId = Guid.NewGuid(), Name = "x", UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.Equal(BusinessViolationCodes.DiagnosisCodeNotFound, response.ValidationResult!.Errors.Single().ErrorCode);
    }

    [Fact]
    public async Task CreateLabAnalyte_DuplicateCode_IsRefused()
    {
        var repository = new FakeAggregateRootRepository<LabAnalyteAggregate>();
        var existing = new LabAnalyteAggregate(Guid.NewGuid());
        existing.Create(new LabAnalyteCreateDto("CREA", "Creatinine", ResultValueKind.Numeric, DiagnosticKind.BloodWork, "mg/dL", 0.8m, 1.8m, null, TestUserContext.Create(), Guid.NewGuid()));
        repository.Seed(existing);

        var response = await new CreateLabAnalyteCommandHandler(repository).HandleAsync(new CreateLabAnalyteCommand
        {
            LabAnalyteId = Guid.NewGuid(), Code = "CREA", Name = "Other", ValueKind = "Numeric", DiagnosticKind = "BloodWork", Unit = "mg/dL",
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.Equal(BusinessViolationCodes.LabAnalyteAlreadyExists, response.ValidationResult!.Errors.Single().ErrorCode);
        Assert.Empty(repository.Saved);
    }

    [Fact]
    public async Task RecordClinicalRecordAccess_WritesOneAuditEntry_InTheAccessCategory_AndNeverPublishes() // U-A5
    {
        var audit = new FakeAggregateRootRepository<AuditEntryAggregate>();
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 10, 7, 9, 0, 0, TimeSpan.Zero));
        var handler = new RecordClinicalRecordAccessCommandHandler(audit, time);
        var catId = Guid.NewGuid();
        var visitId = Guid.NewGuid();
        var user = TestUserContext.Create();

        var response = await handler.HandleAsync(new RecordClinicalRecordAccessCommand
        {
            AuditEntryId = Guid.NewGuid(), CatId = catId, Kind = ClinicalRecordKind.Visit, RecordId = visitId,
            Path = $"/api/staff/visits/{visitId}", IpAddress = "203.0.113.9", UserContext = user, CorrelationId = Guid.NewGuid()
        });

        Assert.True(response.IsSuccess);
        var entry = Assert.Single(audit.Saved);
        Assert.Equal(AuditCategory.ClinicalRecordAccess, entry.Category);
        Assert.Equal(nameof(ClinicalRecordKind.Visit), entry.EntityType);
        Assert.Equal(visitId, entry.EntityId);
        Assert.Equal("ClinicalRecordRead", entry.Action);
        Assert.Equal(user.UserId, entry.ActorUserId);
        Assert.Equal(IPAddress.Parse("203.0.113.9"), entry.IpAddress);
        Assert.Equal(time.GetUtcNow(), entry.OccurredOn);
        Assert.DoesNotContain("?", entry.AfterValue);
        Assert.Contains(ClinicalAuditEntityTypes.All, e => e == entry.EntityType);
    }

    [Fact]
    public async Task RecordClinicalRecordAccess_WithOnlyACatId_UsesTheCatAsTheEntity()
    {
        var audit = new FakeAggregateRootRepository<AuditEntryAggregate>();
        var catId = Guid.NewGuid();

        await new RecordClinicalRecordAccessCommandHandler(audit, new FakeTimeProvider()).HandleAsync(new RecordClinicalRecordAccessCommand
        {
            AuditEntryId = Guid.NewGuid(), CatId = catId, Kind = ClinicalRecordKind.ClinicalHistory, Path = $"/api/staff/cats/{catId}/clinical-history",
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.Equal(catId, Assert.Single(audit.Saved).EntityId);
    }
}
