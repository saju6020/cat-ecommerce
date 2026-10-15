using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Inventory;
using CatEcommerce.Application.CommandHandlers.Inventory;
using CatEcommerce.Application.Commands.Inventory;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Read.Queries.Inventory;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-IMP-001 §12.4 (US-IMP-03, US-IMP-05) -- import consignments of the ACTIVE branch; another branch's is a 404. A consignment is
/// never deleted and its documents are only ever superseded (NFR-LEG-2). Costs and the landed-cost allocation are the accountant's too
/// (<c>Consignments.ManageCosts</c>, OQ-8).</summary>
[ApiController]
[Route("api/staff/procurement/consignments")]
public sealed class ProcurementConsignmentsController(
    IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider, IActiveBranchAccessor activeBranchAccessor) : ControllerBase
{
    [HttpGet]
    [RequireAction(Actions.Procurement.View)]
    public async Task<IActionResult> List(
        [FromQuery] string? status, [FromQuery] string? originCountryCode, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] int? pageNumber, [FromQuery] int? pageSize)
    {
        if (!this.TryResolveBranch(activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var query = new GetConsignmentsQuery
        {
            BranchId = branchId, Status = status, OriginCountryCode = originCountryCode, From = from, To = to,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid()
        };
        if (PagingHelper.ApplyPaging(query, pageNumber, pageSize) is { } invalid) return invalid;

        var response = await dispatcher.GetResultAsync(query);
        return Ok(response.Result);
    }

    [HttpPost]
    [RequireAction(Actions.Procurement.Manage)]
    public async Task<IActionResult> Plan([FromBody] PlanConsignmentRequest request)
    {
        if (!this.TryResolveBranch(activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var consignmentId = Guid.NewGuid();
        var response = await dispatcher.SendAsync(new PlanConsignmentCommand
        {
            ConsignmentId = consignmentId, BranchId = branchId, OriginCountryCode = request.OriginCountryCode, ShipmentReference = request.ShipmentReference,
            PurchaseOrderIds = request.PurchaseOrderIds, HsCodes = request.HsCodes, ImportPermitNumber = request.ImportPermitNumber,
            HealthCertificateNumber = request.HealthCertificateNumber, CustomsDeclarationNumber = request.CustomsDeclarationNumber,
            ExpectedArrivalOn = request.ExpectedArrivalOn, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return this.CreatedOrFailure(response, new { consignmentId, reference = response.Result as string });
    }

    [HttpGet("{id:guid}")]
    [RequireAction(Actions.Procurement.View)]
    public async Task<IActionResult> Get(Guid id)
    {
        if (!this.TryResolveBranch(activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.GetResultAsync(new GetConsignmentQuery
        { ConsignmentId = id, BranchId = branchId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });

        return response.Result is null ? NotFound() : Ok(response.Result);
    }

    /// <summary>US-IMP-03 AC2 -- details, HS codes, numbers, POs, every document (incl. superseded) and the batches received, in one payload.</summary>
    [HttpGet("{id:guid}/inspection-pack")]
    [RequireAction(Actions.Procurement.View)]
    public async Task<IActionResult> GetInspectionPack(Guid id)
    {
        if (!this.TryResolveBranch(activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.GetResultAsync(new GetInspectionPackQuery
        { ConsignmentId = id, BranchId = branchId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });

        return response.Result is null ? NotFound() : Ok(response.Result);
    }

    [HttpPut("{id:guid}")]
    [RequireAction(Actions.Procurement.Manage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateConsignmentRequest request)
    {
        if (!this.TryResolveBranch(activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new UpdateConsignmentDetailsCommand
        {
            ConsignmentId = id, BranchId = branchId, ExpectedVersion = request.ExpectedVersion, OriginCountryCode = request.OriginCountryCode,
            ShipmentReference = request.ShipmentReference, PurchaseOrderIds = request.PurchaseOrderIds, HsCodes = request.HsCodes,
            ImportPermitNumber = request.ImportPermitNumber, HealthCertificateNumber = request.HealthCertificateNumber,
            CustomsDeclarationNumber = request.CustomsDeclarationNumber, ExpectedArrivalOn = request.ExpectedArrivalOn,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return this.NoContentOrFailure(response);
    }

    [HttpPost("{id:guid}/status")]
    [RequireAction(Actions.Procurement.Manage)]
    public async Task<IActionResult> AdvanceStatus(Guid id, [FromBody] AdvanceConsignmentStatusRequest request)
    {
        if (!this.TryResolveBranch(activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new AdvanceConsignmentStatusCommand
        {
            ConsignmentId = id, BranchId = branchId, To = request.To, On = request.On,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return this.NoContentOrFailure(response);
    }

    [HttpPost("{id:guid}/cancel")]
    [RequireAction(Actions.Procurement.Manage)]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelConsignmentRequest request)
    {
        if (!this.TryResolveBranch(activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new CancelConsignmentCommand
        {
            ConsignmentId = id, BranchId = branchId, Reason = request.Reason,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return this.NoContentOrFailure(response);
    }

    [HttpPost("{id:guid}/documents")]
    [RequireAction(Actions.Procurement.Manage)]
    public async Task<IActionResult> AddDocument(Guid id, [FromBody] ConsignmentDocumentRequest request)
    {
        if (!this.TryResolveBranch(activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var documentId = Guid.NewGuid();
        var response = await dispatcher.SendAsync(new AddConsignmentDocumentCommand
        {
            ConsignmentId = id, BranchId = branchId, DocumentId = documentId, Kind = request.Kind, DocumentNumber = request.DocumentNumber, IssuedOn = request.IssuedOn,
            FileName = request.FileName, StorageKey = request.StorageKey, ContentType = request.ContentType, SizeInBytes = request.SizeInBytes,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return this.CreatedOrFailure(response, new { documentId });
    }

    [HttpPost("{id:guid}/documents/{documentId:guid}/supersede")]
    [RequireAction(Actions.Procurement.Manage)]
    public async Task<IActionResult> SupersedeDocument(Guid id, Guid documentId, [FromBody] ConsignmentDocumentRequest request)
    {
        if (!this.TryResolveBranch(activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new SupersedeConsignmentDocumentCommand
        {
            ConsignmentId = id, BranchId = branchId, SupersededDocumentId = documentId, DocumentId = Guid.NewGuid(), Kind = request.Kind,
            DocumentNumber = request.DocumentNumber, IssuedOn = request.IssuedOn, FileName = request.FileName, StorageKey = request.StorageKey,
            ContentType = request.ContentType, SizeInBytes = request.SizeInBytes,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return this.NoContentOrFailure(response);
    }

    [HttpPut("{id:guid}/costs")]
    [RequireAction(Actions.Consignments.ManageCosts)]
    public async Task<IActionResult> ReplaceCosts(Guid id, [FromBody] ReplaceConsignmentCostsRequest request)
    {
        if (!this.TryResolveBranch(activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new ReplaceConsignmentCostsCommand
        {
            ConsignmentId = id, BranchId = branchId, ExpectedVersion = request.ExpectedVersion, CostComponents = request.CostComponents,
            ExchangeRates = request.ExchangeRates, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return this.NoContentOrFailure(response);
    }

    /// <summary>US-IMP-05 -- allocates the consignment's costs across the batches received on it (accepted-quantity basis, largest-remainder rounding).
    /// Only the consignment is written; running it again replaces the allocation and clears the stale flag.</summary>
    [HttpPost("{id:guid}/landed-cost")]
    [RequireAction(Actions.Consignments.ManageCosts)]
    public async Task<IActionResult> AllocateLandedCost(Guid id, [FromBody] AllocateLandedCostRequest request)
    {
        if (!this.TryResolveBranch(activeBranchAccessor, out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new AllocateLandedCostCommand
        {
            ConsignmentId = id, BranchId = branchId, ExpectedVersion = request.ExpectedVersion,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        if (!response.IsSuccess) return this.Failure(response);

        var allocated = (AllocatedLandedCost)response.Result!;
        return Ok(new
        {
            lines = allocated.Lines.Select(l => new
            {
                batchId = l.BatchId, quantity = InventoryViewFormat.Quantity(l.Quantity), purchaseCostBase = InventoryViewFormat.Money(l.PurchaseCostBase, allocated.CurrencyCode),
                allocatedCostBase = InventoryViewFormat.Money(l.AllocatedCostBase, allocated.CurrencyCode), landedUnitCostBase = InventoryViewFormat.UnitCost(l.LandedUnitCostBase, allocated.CurrencyCode)
            }),
            totalCostsBase = InventoryViewFormat.Money(allocated.TotalCostsBase, allocated.CurrencyCode), allocatedOn = allocated.AllocatedOn
        });
    }
}
