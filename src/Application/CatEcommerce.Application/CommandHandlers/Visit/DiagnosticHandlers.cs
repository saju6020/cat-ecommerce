using CatEcommerce.Application.Commands.Visit;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.StoredFile;
using CatEcommerce.Domain.Aggregates.Visit;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Application.CommandHandlers.Visit;

using static VisitCommandHandlerSupport;

/// <summary>
/// The checks every handler that references a stored file makes before it does (HO-EMR-001 §11.4 C14/C15/C19):
/// the file exists, it is the expected clinical purpose uploaded by this caller moments ago
/// (<c>StoredFileAggregate.IsEligibleForAttach</c> -- HO-FIL-001's one rule, which answers every refusal with the
/// same boolean on purpose), and it is not already referenced by another clinical record. The "already attached"
/// pre-check is a courtesy; the unique indexes on <c>file_id</c> are the authority, and the host-wide
/// <c>UniqueConstraintExceptionFilter</c> maps their violation to the same <c>FileAlreadyAttached</c> answer.
/// </summary>
internal static class ClinicalFileAttachment
{
    public static async Task<CommandResponse?> RefuseAsync(
        Guid fileId, FilePurpose purpose, Guid callerId, DateTimeOffset now,
        IAggregateRootRepository<StoredFileAggregate> fileRepository,
        IAggregateRootRepository<VisitAggregate> visitRepository,
        IAggregateRootRepository<DiagnosticOrderAggregate> orderRepository,
        Func<Task<bool>>? alsoReferenced = null)
    {
        var file = await fileRepository.GetByIdAsync(fileId);
        if (file is null)
            return Violation(BusinessViolationCodes.FileNotFound, "FileId", "The file was not found. Upload it again.", nameof(StoredFileAggregate));

        if (!file.IsEligibleForAttach(purpose, callerId, now, FileRules.AttachWindow))
            return Violation(BusinessViolationCodes.FilePurposeMismatch, "FileId",
                $"This file cannot be attached here. Upload it as a {purpose.DisplayName} file and attach it straight away.", nameof(StoredFileAggregate));

        if (await visitRepository.ExistAsync(v => v.Photos.Any(p => p.FileId == fileId))
            || await orderRepository.ExistAsync(o => o.Attachments.Any(a => a.FileId == fileId))
            || (alsoReferenced is not null && await alsoReferenced()))
            return Violation(BusinessViolationCodes.FileAlreadyAttached, "FileId", "This file is already attached to a clinical record.", nameof(StoredFileAggregate));

        return null;
    }
}

/// <summary>C12 -- orders a diagnostic on an open visit of the caller's branch; the order is pending (US-CLN-14 AC1).</summary>
public sealed class OrderDiagnosticCommandHandler(
    IAggregateRootRepository<VisitAggregate> visitRepository,
    IAggregateRootRepository<DiagnosticOrderAggregate> orderRepository,
    IAggregateRootRepository<LabAnalyteAggregate> analyteRepository,
    IBusinessSettings businessSettings,
    IBillingPricingInputs pricingInputs,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<OrderDiagnosticCommand>
{
    public async Task<CommandResponse> HandleAsync(OrderDiagnosticCommand command)
    {
        var visit = await visitRepository.GetByIdAsync(command.VisitId);
        if (visit is null || visit.BranchId != command.BranchId)
            return Violation(BusinessViolationCodes.VisitNotFound, nameof(command.VisitId), ClinicalMessages.VisitNotFound);

        var analytes = new List<RequestedAnalyteDto>(command.AnalyteIds.Count);
        foreach (var analyteId in command.AnalyteIds.Distinct())
        {
            var analyte = await analyteRepository.GetByIdAsync(analyteId);
            if (analyte is null || !analyte.IsActive)
                return Violation(BusinessViolationCodes.LabAnalyteNotFound, nameof(command.AnalyteIds), "A requested analyte was not found or is no longer in use.", nameof(LabAnalyteAggregate));

            analytes.Add(new RequestedAnalyteDto(analyte.Id, analyte.Code, analyte.Name));
        }

        // The test's price is read once, here, and frozen on the order (owner decision 2026-10-14: an admin-set price, default 0). The tax class is
        // the visit's own service's, also frozen; a service that has gone leaves it null and Billing reports the line instead of guessing.
        var kind = AppointmentMapper.ParseEnum<DiagnosticKind>(command.Kind);
        var price = new Money(
            businessSettings.GetDecimal(DiagnosticPricing.SettingKeyFor(kind)),
            businessSettings.GetString(SettingKeys.CommerceCurrencyCode));
        var taxClassId = await pricingInputs.GetServiceTaxClassIdAsync(visit.ClinicServiceId);

        var order = new DiagnosticOrderAggregate(command.DiagnosticOrderId);
        try
        {
            order.Order(new DiagnosticOrderDto(
                visit.Id, visit.CatId, visit.BranchId, visit.Status, kind, command.Description,
                analytes, command.UserContext.UserId, timeProvider.GetUtcNow(), command.UserContext, command.CorrelationId, price, taxClassId));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message, nameof(DiagnosticOrderAggregate));
        }

        await orderRepository.SaveAsync(order, EventPublishOption.InMemoryPublishOnly);
        return Success(order.Id);
    }
}

/// <summary>
/// C13 -- appends one result set to an order (§8.5). Allowed after the visit is finalised: the report arrives
/// days later. The handler applies each analyte's name, unit and range as defaults and <b>stores them on the
/// value</b> -- the lab report's own range wins when it was entered; the aggregate then classifies each value
/// once against the range stored on it.
/// </summary>
public sealed class RecordDiagnosticResultsCommandHandler(
    IAggregateRootRepository<DiagnosticOrderAggregate> orderRepository,
    IAggregateRootRepository<LabAnalyteAggregate> analyteRepository,
    IAggregateRootRepository<BranchAggregate> branchRepository,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<RecordDiagnosticResultsCommand>
{
    public async Task<CommandResponse> HandleAsync(RecordDiagnosticResultsCommand command)
    {
        var order = await orderRepository.GetByIdAsync(command.DiagnosticOrderId);
        if (order is null || order.BranchId != command.BranchId)
            return Violation(BusinessViolationCodes.DiagnosticOrderNotFound, nameof(command.DiagnosticOrderId), "Diagnostic order not found.", nameof(DiagnosticOrderAggregate));

        var values = new List<ResultValueDto>(command.Values.Count);
        foreach (var input in command.Values)
        {
            LabAnalyteAggregate? analyte = null;
            if (input.AnalyteId is { } analyteId)
            {
                analyte = await analyteRepository.GetByIdAsync(analyteId);
                if (analyte is null)
                    return Violation(BusinessViolationCodes.LabAnalyteNotFound, nameof(input.AnalyteId), "An analyte was not found.", nameof(LabAnalyteAggregate));
            }

            var kind = input.ValueKind is { } named ? AppointmentMapper.ParseEnum<ResultValueKind>(named) : analyte?.ValueKind;
            if (kind is null)
                return Violation(BusinessViolationCodes.InvalidDiagnosticResult, nameof(input.ValueKind), "Each value needs a kind: numeric or qualitative.", nameof(DiagnosticOrderAggregate));

            // The report's own range wins when either bound was entered; otherwise the analyte's range is copied.
            var rangeEntered = input.ReferenceLow is not null || input.ReferenceHigh is not null;
            values.Add(new ResultValueDto(
                input.ValueId, input.AnalyteId,
                string.IsNullOrWhiteSpace(input.Name) ? analyte?.Name ?? "" : input.Name,
                kind, input.NumericValue, input.TextValue,
                string.IsNullOrWhiteSpace(input.Unit) ? analyte?.Unit : input.Unit,
                rangeEntered ? input.ReferenceLow : analyte?.ReferenceLow,
                rangeEntered ? input.ReferenceHigh : analyte?.ReferenceHigh,
                string.IsNullOrWhiteSpace(input.NormalText) ? analyte?.NormalText : input.NormalText));
        }

        var branch = await branchRepository.GetByIdAsync(order.BranchId);
        var now = timeProvider.GetUtcNow();
        var today = BranchCalendar.LocalDateOf(now, branch?.TimeZoneId ?? "UTC");

        try
        {
            order.RecordResults(new DiagnosticResultSetDto(
                command.ResultSetId, command.ReportedOn, command.Note, command.SupersedesResultSetId, values,
                command.UserContext.UserId, today, now, command.UserContext, command.CorrelationId));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message, nameof(DiagnosticOrderAggregate));
        }

        await orderRepository.UpdateAsync(order, EventPublishOption.InMemoryPublishOnly);

        var recorded = order.ResultSets.Single(s => s.Id == command.ResultSetId).Values
            .Select(v => new RecordedResultValue(
                v.Id, v.Name, v.ValueKind.DisplayName, v.NumericValue, v.TextValue, v.Unit, v.ReferenceLow, v.ReferenceHigh, v.NormalText, v.Flag.DisplayName))
            .ToList();

        return Success(new RecordResultsResult(command.ResultSetId, recorded));
    }
}

/// <summary>C14 -- attaches a stored lab file to an order (also after the visit is finalised).</summary>
public sealed class AttachDiagnosticFileCommandHandler(
    IAggregateRootRepository<DiagnosticOrderAggregate> orderRepository,
    IAggregateRootRepository<VisitAggregate> visitRepository,
    IAggregateRootRepository<StoredFileAggregate> fileRepository,
    IAggregateRootRepository<BranchAggregate> branchRepository,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<AttachDiagnosticFileCommand>
{
    public async Task<CommandResponse> HandleAsync(AttachDiagnosticFileCommand command)
    {
        var order = await orderRepository.GetByIdAsync(command.DiagnosticOrderId);
        if (order is null || order.BranchId != command.BranchId)
            return Violation(BusinessViolationCodes.DiagnosticOrderNotFound, nameof(command.DiagnosticOrderId), "Diagnostic order not found.", nameof(DiagnosticOrderAggregate));

        var now = timeProvider.GetUtcNow();
        var refusal = await ClinicalFileAttachment.RefuseAsync(
            command.FileId, FilePurpose.LabResult, command.UserContext.UserId, now, fileRepository, visitRepository, orderRepository);
        if (refusal is not null) return refusal;

        var branch = await branchRepository.GetByIdAsync(order.BranchId);
        var today = BranchCalendar.LocalDateOf(now, branch?.TimeZoneId ?? "UTC");

        try
        {
            order.AttachFile(new DiagnosticAttachmentDto(
                command.AttachmentId, command.FileId, command.ResultDate, command.Note, command.UserContext.UserId, today, now, command.UserContext, command.CorrelationId));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message, nameof(DiagnosticOrderAggregate));
        }

        await orderRepository.UpdateAsync(order, EventPublishOption.InMemoryPublishOnly);
        return Success(command.AttachmentId);
    }
}

/// <summary>C14 -- cancels a pending order that has produced nothing yet.</summary>
public sealed class CancelDiagnosticOrderCommandHandler(
    IAggregateRootRepository<DiagnosticOrderAggregate> orderRepository,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<CancelDiagnosticOrderCommand>
{
    public async Task<CommandResponse> HandleAsync(CancelDiagnosticOrderCommand command)
    {
        var order = await orderRepository.GetByIdAsync(command.DiagnosticOrderId);
        if (order is null || order.BranchId != command.BranchId)
            return Violation(BusinessViolationCodes.DiagnosticOrderNotFound, nameof(command.DiagnosticOrderId), "Diagnostic order not found.", nameof(DiagnosticOrderAggregate));

        try
        {
            order.Cancel(new DiagnosticCancelDto(command.Reason, command.UserContext.UserId, timeProvider.GetUtcNow(), command.UserContext, command.CorrelationId));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message, nameof(DiagnosticOrderAggregate));
        }

        await orderRepository.UpdateAsync(order, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

/// <summary>C15 -- attaches a clinical photograph to an open visit (US-CLN-19). The file is a stored file of purpose
/// <c>ClinicalPhoto</c>, referenced once.</summary>
public sealed class AttachClinicalPhotoCommandHandler(
    IAggregateRootRepository<VisitAggregate> visitRepository,
    IAggregateRootRepository<DiagnosticOrderAggregate> orderRepository,
    IAggregateRootRepository<StoredFileAggregate> fileRepository,
    IAggregateRootRepository<BranchAggregate> branchRepository,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<AttachClinicalPhotoCommand>
{
    public async Task<CommandResponse> HandleAsync(AttachClinicalPhotoCommand command)
    {
        var visit = await visitRepository.GetByIdAsync(command.VisitId);
        if (visit is null || visit.BranchId != command.BranchId)
            return Violation(BusinessViolationCodes.VisitNotFound, nameof(command.VisitId), ClinicalMessages.VisitNotFound);

        var now = timeProvider.GetUtcNow();
        var refusal = await ClinicalFileAttachment.RefuseAsync(
            command.FileId, FilePurpose.ClinicalPhoto, command.UserContext.UserId, now, fileRepository, visitRepository, orderRepository);
        if (refusal is not null) return refusal;

        var branch = await branchRepository.GetByIdAsync(visit.BranchId);
        var today = BranchCalendar.LocalDateOf(now, branch?.TimeZoneId ?? "UTC");

        try
        {
            visit.AttachPhoto(new ClinicalPhotoDto(
                command.PhotoId, command.FileId, command.TakenOn, command.Note, command.UserContext.UserId, today, now, command.UserContext, command.CorrelationId));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await visitRepository.UpdateAsync(visit, EventPublishOption.InMemoryPublishOnly);
        return Success(command.PhotoId);
    }
}
