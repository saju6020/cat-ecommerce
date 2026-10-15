using System.Text;
using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Inventory;
using CatEcommerce.Application.CommandHandlers.Inventory;
using CatEcommerce.Application.Commands.Inventory;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Read.Queries.Inventory;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Helpers;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>
/// HO-IMP-001 section 12.4 (US-IMP-11) -- batch recalls. A recall applies to the batch at EVERY location and branch, so these endpoints are not scoped to the
/// active branch; every one needs <c>Recalls.Manage</c> (the trace holds customers' names and contact details, so a nurse can neither recall nor read it).
/// The CSV form of the trace is audited as a <c>BusinessEvent</c>, <c>RecallTraceExported</c>.
/// </summary>
[ApiController]
public sealed class StaffRecallsController(
    IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider, IClientIpAccessor clientIpAccessor) : ControllerBase
{
    private static readonly IReadOnlyList<string> TraceHeaders =
        ["Section", "Date", "Kind", "Quantity", "Location", "Reference", "Customer", "Mobile", "Email", "Anonymised", "Cat", "CatCurrentOwner", "HouseholdCats"];

    [HttpPost("api/staff/inventory/batches/{batchId:guid}/recall")]
    [RequireAction(Actions.Recalls.Manage)]
    public async Task<IActionResult> Declare(Guid batchId, [FromBody] RecallBatchRequest request)
    {
        var response = await dispatcher.SendAsync(new RecallBatchCommand
        {
            BatchId = batchId, ConfirmLotNumber = request.ConfirmLotNumber ?? string.Empty, Reason = request.Reason ?? string.Empty, CustomerAdvice = request.CustomerAdvice ?? string.Empty,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return response.IsSuccess ? StatusCode(201, new { recallNumber = ((DeclaredRecall)response.Result!).RecallNumber }) : this.Failure(response);
    }

    [HttpGet("api/staff/inventory/recalls")]
    [RequireAction(Actions.Recalls.Manage)]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] int? pageNumber, [FromQuery] int? pageSize)
    {
        var query = new GetRecallsQuery { Status = status, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() };
        if (PagingHelper.ApplyPaging(query, pageNumber, pageSize) is { } invalid) return invalid;

        return Ok((await dispatcher.GetResultAsync(query)).Result);
    }

    [HttpGet("api/staff/inventory/recalls/{batchId:guid}")]
    [RequireAction(Actions.Recalls.Manage)]
    public async Task<IActionResult> Get(Guid batchId)
    {
        var response = await dispatcher.GetResultAsync(new GetRecallQuery { BatchId = batchId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });
        return response.Result is null ? NotFound() : Ok(response.Result);
    }

    [HttpGet("api/staff/inventory/recalls/{batchId:guid}/trace")]
    [RequireAction(Actions.Recalls.Manage)]
    public async Task<IActionResult> Trace(Guid batchId)
    {
        var response = await dispatcher.GetResultAsync(TraceQuery(batchId));
        return response.Result is null ? NotFound() : Ok(response.Result);
    }

    [HttpGet("api/staff/inventory/recalls/{batchId:guid}/trace.csv")]
    [RequireAction(Actions.Recalls.Manage)]
    public async Task<IActionResult> TraceCsv(Guid batchId)
    {
        var trace = (await dispatcher.GetResultAsync(TraceQuery(batchId))).Result;
        if (trace is null) return NotFound();

        var rows = TraceRows(trace);
        var audit = await dispatcher.SendAsync(new RecordRecallTraceExportCommand
        {
            BatchId = batchId, RowCount = rows.Count, IpAddress = clientIpAccessor.GetClientIpAddress()?.ToString(),
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });
        if (!audit.IsSuccess) return this.Failure(audit); // no file leaves unaudited

        return File(Encoding.UTF8.GetBytes(InventoryCsv.Build(TraceHeaders, rows)), "text/csv", $"recall-trace-{trace.RecallNumber}.csv");
    }

    [HttpPost("api/staff/inventory/recalls/{batchId:guid}/notify")]
    [RequireAction(Actions.Recalls.Manage)]
    public async Task<IActionResult> Notify(Guid batchId)
    {
        var response = await dispatcher.SendAsync(new NotifyRecallRecipientsCommand
        { BatchId = batchId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true });

        if (!response.IsSuccess) return this.Failure(response);

        var run = (RecallNotificationRun)response.Result!;
        return Ok(new { requested = run.Requested, alreadyNotified = run.AlreadyNotified, remaining = run.Remaining });
    }

    [HttpPost("api/staff/inventory/recalls/{batchId:guid}/withdraw")]
    [RequireAction(Actions.Recalls.Manage)]
    public async Task<IActionResult> Withdraw(Guid batchId, [FromBody] RecallBatchReasonRequest request) =>
        this.NoContentOrFailure(await dispatcher.SendAsync(new WithdrawBatchRecallCommand
        { BatchId = batchId, Reason = request.Reason ?? string.Empty, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true }));

    [HttpPost("api/staff/inventory/recalls/{batchId:guid}/close")]
    [RequireAction(Actions.Recalls.Manage)]
    public async Task<IActionResult> Close(Guid batchId, [FromBody] RecallClosureRequest request) =>
        this.NoContentOrFailure(await dispatcher.SendAsync(new CloseBatchRecallCommand
        { BatchId = batchId, Note = request.Note ?? string.Empty, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true }));

    private GetRecallTraceQuery TraceQuery(Guid batchId) => new() { BatchId = batchId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() };

    private static List<IReadOnlyList<object?>> TraceRows(RecallTraceViewModel trace)
    {
        var rows = new List<IReadOnlyList<object?>>();

        foreach (var r in trace.Received)
            rows.Add(new object?[]
            {
                "Received", r.OccurredOn, r.Kind, r.Quantity, r.LocationCode, r.ReferenceLabel, r.Customer?.Name, r.Customer?.MobileNumber, r.Customer?.Email, r.Customer?.IsAnonymised,
                r.Cat?.Name, r.Cat?.CurrentOwner?.Name, string.Join("; ", r.HouseholdCats)
            });

        foreach (var r in trace.Returned)
            rows.Add(new object?[] { "Returned", null, r.ReferenceKind, r.Quantity, null, r.ReferenceLabel, r.Customer?.Name, r.Customer?.MobileNumber, r.Customer?.Email, r.Customer?.IsAnonymised, null, null, null });

        foreach (var r in trace.UnshippedDispatchBlocked)
            rows.Add(new object?[]
            {
                "UnshippedDispatchBlocked", null, r.ReferenceKind, r.Quantity, null, r.ReferenceId.ToString(), r.Customer?.Name, r.Customer?.MobileNumber, r.Customer?.Email,
                r.Customer?.IsAnonymised, r.Cat?.Name, r.Cat?.CurrentOwner?.Name, null
            });

        rows.Add(new object?[] { "Unidentified", null, null, trace.Unidentified.Quantity, null, $"{trace.Unidentified.Count} sale(s)", null, null, null, null, null, null, null });

        foreach (var h in trace.StockHeld)
            rows.Add(new object?[] { "StockHeld", null, null, h.OnHand, h.LocationCode, null, null, null, null, null, null, null, null });

        return rows;
    }
}
