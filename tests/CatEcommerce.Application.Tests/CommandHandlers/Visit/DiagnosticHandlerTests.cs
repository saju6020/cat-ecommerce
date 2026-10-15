using CatEcommerce.Application.CommandHandlers.Visit;
using CatEcommerce.Application.Commands.Visit;
using CatEcommerce.Application.Tests.TestSupport;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.StoredFile;
using CatEcommerce.Domain.Aggregates.Visit;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Time.Testing;
using Platform.Infrastructure.Common.Security;
using Xunit;

namespace CatEcommerce.Application.Tests.CommandHandlers.Visit;

/// <summary>HO-EMR-001 §15 Phase D -- U-D2 (analyte defaults), U-D3..U-D5 at handler level, the file checks of C14/C15 and the
/// branch rule. The aggregate's own cases (U-D1, U-D4, U-D10) are in the Domain tests.</summary>
public sealed class DiagnosticHandlerTests
{
    private static readonly DateTimeOffset Now = ClinicalTestData.Now;

    private sealed class World
    {
        public Guid BranchId { get; } = Guid.NewGuid();
        public FakeAggregateRootRepository<VisitAggregate> Visits { get; } = new();
        public FakeAggregateRootRepository<DiagnosticOrderAggregate> Orders { get; } = new();
        public FakeAggregateRootRepository<LabAnalyteAggregate> Analytes { get; } = new();
        public FakeAggregateRootRepository<StoredFileAggregate> Files { get; } = new();
        public FakeAggregateRootRepository<BranchAggregate> Branches { get; } = new();
        public FakeTimeProvider Clock { get; } = new(Now);
        public VisitAggregate Visit { get; }

        // HO-BIL-001 Phase J: the price comes from the settings registry and the tax class from the visit's own service.
        public FakeServicePricing Pricing { get; } = new();
        public Guid ServiceTaxClassId { get; } = Guid.NewGuid();
        public Dictionary<string, object> PriceSettings { get; } = new() { [SettingKeys.CommerceCurrencyCode] = "BDT" };

        public OrderDiagnosticCommandHandler OrderHandler() => new(Visits, Orders, Analytes,
            new CatEcommerce.Infrastructure.Fakes.Administration.InMemoryBusinessSettings(PriceSettings), Pricing, Clock);

        public World()
        {
            var branch = ClinicalTestData.Branch();
            // The branch id is the seeded branch's own, so the handlers' branch lookup resolves "Asia/Dhaka".
            Branches.Seed(branch);
            BranchId = branch.Id;
            Visit = ClinicalTestData.OpenVisit(BranchId, Guid.NewGuid(), Guid.NewGuid());
            Visits.Seed(Visit);
            Pricing.Services[Visit.ClinicServiceId] = new ServicePriceInfo(Visit.ClinicServiceId, "Consultation", true, new Money(800m, "BDT"), ServiceTaxClassId);
        }

        public LabAnalyteAggregate Analyte(string code, decimal? low = 5m, decimal? high = 10m, string? unit = "g/dL", bool active = true)
        {
            var analyte = new LabAnalyteAggregate(Guid.NewGuid());
            analyte.Create(new LabAnalyteCreateDto(code, "Name of " + code, ResultValueKind.Numeric, DiagnosticKind.BloodWork, unit, low, high, null, TestUserContext.Create(), Guid.NewGuid()));
            if (!active) analyte.SetActive(new CatalogueActiveDto(false, TestUserContext.Create(), Guid.NewGuid()));
            Analytes.Seed(analyte);
            return analyte;
        }

        public StoredFileAggregate File(FilePurpose purpose, Guid uploadedBy, DateTimeOffset? uploadedOn = null, string contentType = "application/pdf")
        {
            var file = new StoredFileAggregate(Guid.NewGuid());
            file.Register(new StoredFileRegistrationDto(
                file.Id, purpose, contentType, 1000, new string('a', 64), "report.pdf", $"{purpose.DisplayName}/2026/10/{file.Id:N}",
                uploadedBy, uploadedOn ?? Now, "EicarTest", null, TestUserContext.Create(), Guid.NewGuid()));
            Files.Seed(file);
            return file;
        }

        public DiagnosticOrderAggregate Order(DiagnosticKind? kind = null)
        {
            var order = new DiagnosticOrderAggregate(Guid.NewGuid());
            order.Order(new DiagnosticOrderDto(Visit.Id, Visit.CatId, BranchId, VisitStatus.Open, kind ?? DiagnosticKind.BloodWork, null, [],
                Guid.NewGuid(), Now, TestUserContext.Create(), Guid.NewGuid()));
            Orders.Seed(order);
            return order;
        }

        public RecordDiagnosticResultsCommandHandler ResultsHandler() => new(Orders, Analytes, Branches, Clock);

        public AttachDiagnosticFileCommandHandler AttachHandler() => new(Orders, Visits, Files, Branches, Clock);

        public AttachClinicalPhotoCommandHandler PhotoHandler() => new(Visits, Orders, Files, Branches, Clock);
    }

    private static RecordDiagnosticResultsCommand ResultsCommand(DiagnosticOrderAggregate order, Guid branchId, params ResultValueInput[] values) => new()
    {
        DiagnosticOrderId = order.Id, ResultSetId = Guid.NewGuid(), BranchId = branchId, ReportedOn = new DateOnly(2026, 10, 7), Values = values,
        UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
    };

    private static ResultValueInput Value(Guid? analyteId = null, string? name = null, string? kind = "Numeric", decimal? numeric = null, string? text = null,
        string? unit = null, decimal? low = null, decimal? high = null, string? normalText = null) => new()
    {
        ValueId = Guid.NewGuid(), AnalyteId = analyteId, Name = name, ValueKind = kind, NumericValue = numeric, TextValue = text, Unit = unit,
        ReferenceLow = low, ReferenceHigh = high, NormalText = normalText
    };

    // ── C12 ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Order_SnapshotsTheRequestedAnalytes_AndIsPending() // US-CLN-14 AC1
    {
        var world = new World();
        var analyte = world.Analyte("HB");
        var handler = world.OrderHandler();

        var response = await handler.HandleAsync(new OrderDiagnosticCommand
        {
            DiagnosticOrderId = Guid.NewGuid(), VisitId = world.Visit.Id, BranchId = world.BranchId, Kind = "BloodWork", AnalyteIds = [analyte.Id, analyte.Id],
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.True(response.IsSuccess);
        var order = Assert.Single(world.Orders.Saved);
        Assert.Equal(DiagnosticOrderStatus.Pending, order.Status);
        var requested = Assert.Single(order.RequestedAnalytes);
        Assert.Equal("HB", requested.Code);
        Assert.Equal(world.Visit.CatId, order.CatId);
    }

    // HO-BIL-001 Phase J (owner decision 2026-10-14: "add a price, charge them", admin-set, default 0).
    [Fact]
    public async Task Order_FreezesTheKindsAdminPrice_AndTheVisitServicesTaxClass_OnTheOrder()
    {
        var world = new World();
        world.PriceSettings[SettingKeys.ClinicalDiagnosticPriceBloodWork] = 350m;
        var command = new OrderDiagnosticCommand
        {
            DiagnosticOrderId = Guid.NewGuid(), VisitId = world.Visit.Id, BranchId = world.BranchId, Kind = "BloodWork",
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        };

        Assert.True((await world.OrderHandler().HandleAsync(command)).IsSuccess);

        var order = Assert.Single(world.Orders.Saved);
        Assert.Equal(new Money(350m, "BDT"), order.UnitPrice);
        Assert.Equal(world.ServiceTaxClassId, order.TaxClassId);

        // The admin changes the price afterwards: the order that was already made keeps what it was made at.
        world.PriceSettings[SettingKeys.ClinicalDiagnosticPriceBloodWork] = 999m;
        Assert.Equal(350m, order.UnitPrice!.Amount);
    }

    [Fact]
    public async Task Order_OfAKindNobodyPriced_IsPricedZero_NotLeftWithoutAPrice()
    {
        var world = new World();

        await world.OrderHandler().HandleAsync(new OrderDiagnosticCommand
        {
            DiagnosticOrderId = Guid.NewGuid(), VisitId = world.Visit.Id, BranchId = world.BranchId, Kind = "Urinalysis",
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.Equal(new Money(0m, "BDT"), Assert.Single(world.Orders.Saved).UnitPrice);
    }

    [Fact]
    public async Task Order_WhenTheVisitsServiceHasNoTaxClass_KeepsTheTaxClassNull_SoBillingReportsItInsteadOfGuessing()
    {
        var world = new World();
        world.Pricing.Services.Clear();

        await world.OrderHandler().HandleAsync(new OrderDiagnosticCommand
        {
            DiagnosticOrderId = Guid.NewGuid(), VisitId = world.Visit.Id, BranchId = world.BranchId, Kind = "BloodWork",
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.Null(Assert.Single(world.Orders.Saved).TaxClassId);
    }

    [Fact]
    public async Task Order_OnAFinalisedVisit_IsRefused_AndAnInactiveAnalyteIsRefused()
    {
        var world = new World();
        var inactive = world.Analyte("OLD", active: false);
        var handler = world.OrderHandler();

        var unusable = await handler.HandleAsync(new OrderDiagnosticCommand
        {
            DiagnosticOrderId = Guid.NewGuid(), VisitId = world.Visit.Id, BranchId = world.BranchId, Kind = "BloodWork", AnalyteIds = [inactive.Id],
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });
        Assert.Equal(BusinessViolationCodes.LabAnalyteNotFound, unusable.ValidationResult!.Errors.Single().ErrorCode);

        var imagingWithoutDescription = await handler.HandleAsync(new OrderDiagnosticCommand
        {
            DiagnosticOrderId = Guid.NewGuid(), VisitId = world.Visit.Id, BranchId = world.BranchId, Kind = "Imaging",
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });
        Assert.Equal(BusinessViolationCodes.DiagnosticDescriptionRequired, imagingWithoutDescription.ValidationResult!.Errors.Single().ErrorCode);

        var visit = world.Visit;
        var code = new DiagnosisCodeAggregate(Guid.NewGuid());
        code.Create(new DiagnosisCodeCreateDto("X", "x", null, null, null, TestUserContext.Create(), Guid.NewGuid()));
        visit.SaveNote(new SoapNoteDto("s", "o", "a", "p", "", [new ProblemDto(Guid.NewGuid(), code.Id, "X", "x", true, ProblemCertainty.Confirmed, null)], null, null,
            Guid.NewGuid(), Now, TestUserContext.Create(), Guid.NewGuid()));
        visit.Finalise(new VisitFinalisationDto([StaffRoles.Vet], Guid.NewGuid(), Now, TestUserContext.Create(), Guid.NewGuid(), Guid.NewGuid()));

        var onFinalised = await handler.HandleAsync(new OrderDiagnosticCommand
        {
            DiagnosticOrderId = Guid.NewGuid(), VisitId = world.Visit.Id, BranchId = world.BranchId, Kind = "BloodWork",
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });
        Assert.Equal(BusinessViolationCodes.VisitFinalised, onFinalised.ValidationResult!.Errors.Single().ErrorCode);
    }

    // ── C13 ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RecordResults_CopiesTheAnalytesRangeAndUnit_WhenNoneWasEntered_AndFlagsAgainstIt() // U-D2
    {
        var world = new World();
        var analyte = world.Analyte("HB", low: 5m, high: 10m, unit: "g/dL");
        var order = world.Order();

        var response = await world.ResultsHandler().HandleAsync(ResultsCommand(order, world.BranchId,
            Value(analyte.Id, numeric: 4.9m), Value(analyte.Id, numeric: 10.0m), Value(analyte.Id, numeric: 10.1m)));

        Assert.True(response.IsSuccess);
        var result = Assert.IsType<RecordResultsResult>(response.Result);
        Assert.Equal(["Low", "Normal", "High"], result.Values.Select(v => v.Flag));
        Assert.All(result.Values, v =>
        {
            Assert.Equal("g/dL", v.Unit);
            Assert.Equal(5m, v.ReferenceLow);
            Assert.Equal(10m, v.ReferenceHigh);
            Assert.Equal("Name of HB", v.Name);
        });
        Assert.Equal(DiagnosticOrderStatus.Resulted, order.Status);
    }

    [Fact]
    public async Task RecordResults_TheLabReportsOwnRangeWins_WhenEntered() // FR-CLN-16
    {
        var world = new World();
        var analyte = world.Analyte("HB", low: 5m, high: 10m);
        var order = world.Order();

        var response = await world.ResultsHandler().HandleAsync(ResultsCommand(order, world.BranchId,
            Value(analyte.Id, numeric: 4.9m, low: 4m, high: 9m, unit: "g/L")));

        var value = Assert.Single(Assert.IsType<RecordResultsResult>(response.Result).Values);
        Assert.Equal("Normal", value.Flag);
        Assert.Equal((4m, 9m, "g/L"), (value.ReferenceLow, value.ReferenceHigh, value.Unit));
    }

    [Fact]
    public async Task RecordResults_QualitativeValues_AreComparedWithTheNormalText() // U-D1
    {
        var world = new World();
        var order = world.Order(DiagnosticKind.FelvFivTest);

        var response = await world.ResultsHandler().HandleAsync(ResultsCommand(order, world.BranchId,
            Value(name: "FeLV", kind: "Qualitative", text: "negative", normalText: "Negative"),
            Value(name: "FIV", kind: "Qualitative", text: "Positive", normalText: "Negative"),
            Value(name: "Other", kind: "Qualitative", text: "Seen")));

        Assert.Equal(["Normal", "Abnormal", "NotAssessed"], Assert.IsType<RecordResultsResult>(response.Result).Values.Select(v => v.Flag));
    }

    [Fact]
    public async Task RecordResults_Superseding_ASetOfAnotherOrder_IsRefused() // U-D3
    {
        var world = new World();
        var analyte = world.Analyte("HB");
        var first = world.Order();
        var second = world.Order();
        var firstSet = ResultsCommand(first, world.BranchId, Value(analyte.Id, numeric: 7m));
        await world.ResultsHandler().HandleAsync(firstSet);

        var command = new RecordDiagnosticResultsCommand
        {
            DiagnosticOrderId = second.Id, ResultSetId = Guid.NewGuid(), BranchId = world.BranchId, ReportedOn = new DateOnly(2026, 10, 7),
            Values = [Value(analyte.Id, numeric: 7m)], SupersedesResultSetId = firstSet.ResultSetId,
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        };
        var response = await world.ResultsHandler().HandleAsync(command);

        Assert.Equal(BusinessViolationCodes.ResultSetNotFound, response.ValidationResult!.Errors.Single().ErrorCode);
        Assert.Empty(second.ResultSets);
    }

    [Fact]
    public async Task RecordResults_AfterTheVisitIsFinalised_IsAllowed_AndNothingElseTouchesTheVisit() // U-D5
    {
        var world = new World();
        var analyte = world.Analyte("HB");
        var order = world.Order();
        var code = new DiagnosisCodeAggregate(Guid.NewGuid());
        code.Create(new DiagnosisCodeCreateDto("X", "x", null, null, null, TestUserContext.Create(), Guid.NewGuid()));
        world.Visit.SaveNote(new SoapNoteDto("s", "o", "a", "p", "", [new ProblemDto(Guid.NewGuid(), code.Id, "X", "x", true, ProblemCertainty.Confirmed, null)], null, null,
            Guid.NewGuid(), Now, TestUserContext.Create(), Guid.NewGuid()));
        world.Visit.Finalise(new VisitFinalisationDto([StaffRoles.Vet], Guid.NewGuid(), Now, TestUserContext.Create(), Guid.NewGuid(), Guid.NewGuid()));

        var response = await world.ResultsHandler().HandleAsync(ResultsCommand(order, world.BranchId, Value(analyte.Id, numeric: 7m)));

        Assert.True(response.IsSuccess);
        Assert.Empty(world.Visits.Updated);
    }

    [Fact]
    public async Task RecordResults_ForAnotherBranchsOrder_IsNotFound()
    {
        var world = new World();
        var order = world.Order();

        var response = await world.ResultsHandler().HandleAsync(ResultsCommand(order, Guid.NewGuid(), Value(name: "x", numeric: 1m, unit: "u")));

        Assert.Equal(BusinessViolationCodes.DiagnosticOrderNotFound, response.ValidationResult!.Errors.Single().ErrorCode);
    }

    // ── C14 / C15 files ────────────────────────────────────────────────────────────────────────

    private static AttachDiagnosticFileCommand AttachCommand(DiagnosticOrderAggregate order, Guid branchId, Guid fileId, UserContext user) => new()
    {
        DiagnosticOrderId = order.Id, AttachmentId = Guid.NewGuid(), BranchId = branchId, FileId = fileId,
        ResultDate = new DateOnly(2026, 10, 7), Note = "CBC report", UserContext = user, CorrelationId = Guid.NewGuid()
    };

    [Fact]
    public async Task AttachFile_ALabFileUploadedByTheCaller_IsAttached() // US-CLN-14 AC3
    {
        var world = new World();
        var order = world.Order();
        var user = TestUserContext.Create();
        var file = world.File(FilePurpose.LabResult, user.UserId);

        var response = await world.AttachHandler().HandleAsync(AttachCommand(order, world.BranchId, file.Id, user));

        Assert.True(response.IsSuccess);
        Assert.Equal(file.Id, Assert.Single(order.Attachments).FileId);
    }

    [Fact]
    public async Task AttachFile_Refusals_UnknownFile_WrongPurpose_SomeoneElsesFile_ATooOldFile_AndAFileAlreadyAttached()
    {
        var world = new World();
        var order = world.Order();
        var user = TestUserContext.Create();

        var unknown = await world.AttachHandler().HandleAsync(AttachCommand(order, world.BranchId, Guid.NewGuid(), user));
        Assert.Equal(BusinessViolationCodes.FileNotFound, unknown.ValidationResult!.Errors.Single().ErrorCode);

        var photo = world.File(FilePurpose.ClinicalPhoto, user.UserId, contentType: "image/png");
        var wrongPurpose = await world.AttachHandler().HandleAsync(AttachCommand(order, world.BranchId, photo.Id, user));
        Assert.Equal(BusinessViolationCodes.FilePurposeMismatch, wrongPurpose.ValidationResult!.Errors.Single().ErrorCode);

        var othersFile = world.File(FilePurpose.LabResult, Guid.NewGuid());
        var notYours = await world.AttachHandler().HandleAsync(AttachCommand(order, world.BranchId, othersFile.Id, user));
        Assert.Equal(BusinessViolationCodes.FilePurposeMismatch, notYours.ValidationResult!.Errors.Single().ErrorCode);

        var old = world.File(FilePurpose.LabResult, user.UserId, uploadedOn: Now - FileRules.AttachWindow - TimeSpan.FromMinutes(1));
        var tooOld = await world.AttachHandler().HandleAsync(AttachCommand(order, world.BranchId, old.Id, user));
        Assert.Equal(BusinessViolationCodes.FilePurposeMismatch, tooOld.ValidationResult!.Errors.Single().ErrorCode);

        var good = world.File(FilePurpose.LabResult, user.UserId);
        Assert.True((await world.AttachHandler().HandleAsync(AttachCommand(order, world.BranchId, good.Id, user))).IsSuccess);
        var again = await world.AttachHandler().HandleAsync(AttachCommand(order, world.BranchId, good.Id, user));
        Assert.Equal(BusinessViolationCodes.FileAlreadyAttached, again.ValidationResult!.Errors.Single().ErrorCode);
        Assert.Single(order.Attachments);
    }

    [Fact]
    public async Task AttachPhoto_RequiresAClinicalPhotoFile_ADateNotInTheFuture_AndOneAttachmentPerFile() // U-D10, I-D11
    {
        var world = new World();
        var user = TestUserContext.Create();
        var photo = world.File(FilePurpose.ClinicalPhoto, user.UserId, contentType: "image/jpeg");

        AttachClinicalPhotoCommand Command(Guid fileId, DateOnly takenOn) => new()
        {
            VisitId = world.Visit.Id, PhotoId = Guid.NewGuid(), BranchId = world.BranchId, FileId = fileId, TakenOn = takenOn, Note = "Left ear",
            UserContext = user, CorrelationId = Guid.NewGuid()
        };

        var tomorrow = await world.PhotoHandler().HandleAsync(Command(photo.Id, new DateOnly(2026, 10, 8)));
        Assert.Equal(BusinessViolationCodes.ClinicalDateInFuture, tomorrow.ValidationResult!.Errors.Single().ErrorCode);

        Assert.True((await world.PhotoHandler().HandleAsync(Command(photo.Id, new DateOnly(2026, 10, 7)))).IsSuccess);
        Assert.Single(world.Visit.Photos);

        var twice = await world.PhotoHandler().HandleAsync(Command(photo.Id, new DateOnly(2026, 10, 7)));
        Assert.Equal(BusinessViolationCodes.FileAlreadyAttached, twice.ValidationResult!.Errors.Single().ErrorCode);

        var lab = world.File(FilePurpose.LabResult, user.UserId);
        var wrong = await world.PhotoHandler().HandleAsync(Command(lab.Id, new DateOnly(2026, 10, 7)));
        Assert.Equal(BusinessViolationCodes.FilePurposeMismatch, wrong.ValidationResult!.Errors.Single().ErrorCode);
    }

    [Fact]
    public async Task Cancel_AfterResults_IsRefused_ForAPendingOrder_Succeeds() // U-D4
    {
        var world = new World();
        var handler = new CancelDiagnosticOrderCommandHandler(world.Orders, world.Clock);
        var pending = world.Order();
        var withResults = world.Order();
        await world.ResultsHandler().HandleAsync(ResultsCommand(withResults, world.BranchId, Value(name: "x", numeric: 1m, unit: "u")));

        var refused = await handler.HandleAsync(new CancelDiagnosticOrderCommand
        {
            DiagnosticOrderId = withResults.Id, BranchId = world.BranchId, Reason = "wrong cat", UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });
        Assert.Equal(BusinessViolationCodes.DiagnosticOrderHasResults, refused.ValidationResult!.Errors.Single().ErrorCode);

        var cancelled = await handler.HandleAsync(new CancelDiagnosticOrderCommand
        {
            DiagnosticOrderId = pending.Id, BranchId = world.BranchId, Reason = "not needed", UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });
        Assert.True(cancelled.IsSuccess);
        Assert.Equal(DiagnosticOrderStatus.Cancelled, pending.Status);
    }
}
