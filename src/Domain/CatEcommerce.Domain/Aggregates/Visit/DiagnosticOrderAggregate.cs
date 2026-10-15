using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Entities;
using CatEcommerce.Domain.Events.Visit;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Visit;

/// <summary>
/// HO-EMR-001 §8.5 / ADR-124 -- a diagnostic order and what comes back for it. Its own root, not part
/// of the visit: results arrive <i>days</i> later, after the visit is finalised, and must keep arriving.
/// Results and attachments are <b>append-only</b>: a correction is a new result set naming the one it
/// supersedes (the earlier set stays visible); the database refuses any <c>UPDATE</c> or
/// <c>DELETE</c> of a result set, value or attachment (ADR-118).
/// </summary>
public sealed class DiagnosticOrderAggregate : AggregateRoot
{
    private readonly List<DiagnosticRequestedAnalyte> _requestedAnalytes = [];
    private readonly List<DiagnosticResultSet> _resultSets = [];
    private readonly List<DiagnosticAttachment> _attachments = [];

    public Guid VisitId { get; private set; }
    public Guid CatId { get; private set; }
    public Guid BranchId { get; private set; }
    public DiagnosticKind Kind { get; private set; } = default!;
    public string? Description { get; private set; }
    public Guid OrderedBy { get; private set; }
    public DateTimeOffset OrderedOn { get; private set; }
    public DiagnosticOrderStatus Status { get; private set; } = DiagnosticOrderStatus.Pending;
    public string? CancelledReason { get; private set; }
    public DateTimeOffset? CancelledOn { get; private set; }
    public Guid? CancelledBy { get; private set; }

    /// <summary>The price of this test <b>as it stood when it was ordered</b> (HO-BIL-001 Phase J, owner decision 2026-10-14: each test has an admin-set price,
    /// default 0), snapshotted like a treatment's (ADR-127) and never re-read. Null on an order made before Phase J: Billing does not bill what has no price
    /// recorded, and does not invent one.</summary>
    public Money? UnitPrice => UnitPriceAmount is { } amount && UnitPriceCurrency is { } currency ? new Money(amount, currency) : null;

    public decimal? UnitPriceAmount { get; private set; }
    public string? UnitPriceCurrency { get; private set; }

    /// <summary>The tax class the test is billed under -- the visit's own service's class at ordering (a snapshot, like the price).</summary>
    public Guid? TaxClassId { get; private set; }

    public IReadOnlyList<DiagnosticRequestedAnalyte> RequestedAnalytes => _requestedAnalytes;
    public IReadOnlyList<DiagnosticResultSet> ResultSets => _resultSets;
    public IReadOnlyList<DiagnosticAttachment> Attachments => _attachments;

    private DiagnosticOrderAggregate() { }

    public DiagnosticOrderAggregate(Guid id) : base(id) { }

    /// <summary>The order is <b>pending</b> from the moment it is recorded (US-CLN-14 AC1). The visit
    /// must be open -- results, by contrast, are accepted after it is finalised.</summary>
    public void Order(DiagnosticOrderDto dto)
    {
        if (dto.VisitStatus != VisitStatus.Open)
            throw Violation(BusinessViolationCodes.VisitFinalised, ClinicalMessages.VisitFinalised, nameof(dto.VisitStatus));

        var needsDescription = dto.Kind == DiagnosticKind.Other || dto.Kind == DiagnosticKind.Imaging;
        if (needsDescription && string.IsNullOrWhiteSpace(dto.Description))
            throw Violation(BusinessViolationCodes.DiagnosticDescriptionRequired, $"Describe the {dto.Kind.DisplayName} study to be done.", nameof(dto.Description));

        if (dto.Description is { Length: > DiagnosticRules.MaxDescriptionLength })
            throw Violation(BusinessViolationCodes.ClinicalTextTooLong, "The description is too long.", nameof(dto.Description));

        if (dto.Analytes.Count > DiagnosticRules.MaxRequestedAnalytes)
            throw Violation(BusinessViolationCodes.ClinicalLimitExceeded, $"An order lists at most {DiagnosticRules.MaxRequestedAnalytes} analytes.", nameof(dto.Analytes));

        VisitId = dto.VisitId;
        CatId = dto.CatId;
        BranchId = dto.BranchId;
        Kind = dto.Kind;
        Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description;
        OrderedBy = dto.OrderedBy;
        OrderedOn = dto.Now;
        Status = DiagnosticOrderStatus.Pending;
        UnitPriceAmount = dto.UnitPrice?.Amount;
        UnitPriceCurrency = dto.UnitPrice?.CurrencyCode;
        TaxClassId = dto.UnitPrice is null ? null : dto.TaxClassId;

        foreach (var analyte in dto.Analytes.DistinctBy(a => a.AnalyteId))
            _requestedAnalytes.Add(new DiagnosticRequestedAnalyte(Guid.NewGuid(), analyte.AnalyteId, analyte.Code, analyte.Name));

        SetDefaultValue(dto.UserContext);
        AddDomainEvent(new DiagnosticOrderedEvent
        {
            DiagnosticOrderId = Id, VisitId = VisitId, CatId = CatId, Kind = Kind,
            CorrelationId = dto.CorrelationId, UserContext = dto.UserContext
        });
        UpdateVersion();
    }

    /// <summary>Appends one result set; every value is classified once, against the range stored on it.
    /// <see cref="DiagnosticResultSetDto.SupersedesResultSetId"/>, when given, must be a set of
    /// <i>this</i> order.</summary>
    public void RecordResults(DiagnosticResultSetDto dto)
    {
        EnsureNotCancelled();

        if (_resultSets.Count >= DiagnosticRules.MaxResultSets)
            throw Violation(BusinessViolationCodes.ClinicalLimitExceeded, $"An order holds at most {DiagnosticRules.MaxResultSets} result sets.", nameof(ResultSets));

        if (dto.Values.Count < 1 || dto.Values.Count > DiagnosticRules.MaxResultValuesPerSet)
            throw Violation(BusinessViolationCodes.InvalidDiagnosticResult, $"A result set has between 1 and {DiagnosticRules.MaxResultValuesPerSet} values.", nameof(dto.Values));

        if (dto.ReportedOn > dto.Today)
            throw Violation(BusinessViolationCodes.ClinicalDateInFuture, "The report date cannot be in the future.", nameof(dto.ReportedOn));

        if (dto.Note is { Length: > DiagnosticRules.MaxResultNoteLength })
            throw Violation(BusinessViolationCodes.ClinicalTextTooLong, "The note is too long.", nameof(dto.Note));

        if (dto.SupersedesResultSetId is { } supersedes && _resultSets.All(s => s.Id != supersedes))
            throw Violation(BusinessViolationCodes.ResultSetNotFound, "The result set to supersede was not found on this order.", nameof(dto.SupersedesResultSetId));

        var values = dto.Values.Select(ToValue).ToList();

        _resultSets.Add(new DiagnosticResultSet(dto.ResultSetId, dto.ReportedOn, dto.Note, dto.SupersedesResultSetId, dto.RecordedBy, dto.Now, values));
        Status = DiagnosticOrderStatus.Resulted;

        SetDefaultValue(dto.UserContext);
        AddDomainEvent(new DiagnosticResultsRecordedEvent
        {
            DiagnosticOrderId = Id, ResultSetId = dto.ResultSetId, CatId = CatId,
            HasAbnormal = values.Any(v => v.Flag == ResultFlag.Low || v.Flag == ResultFlag.High || v.Flag == ResultFlag.Abnormal),
            CorrelationId = dto.CorrelationId, UserContext = dto.UserContext
        });
        UpdateVersion();
    }

    /// <summary>The file's purpose (<c>LabResult</c>) and "attached once" are the handler's checks.</summary>
    public void AttachFile(DiagnosticAttachmentDto dto)
    {
        EnsureNotCancelled();

        if (_attachments.Count >= DiagnosticRules.MaxAttachments)
            throw Violation(BusinessViolationCodes.ClinicalLimitExceeded, $"An order holds at most {DiagnosticRules.MaxAttachments} files.", nameof(Attachments));

        if (dto.ResultDate > dto.Today)
            throw Violation(BusinessViolationCodes.ClinicalDateInFuture, "The result date cannot be in the future.", nameof(dto.ResultDate));

        if (dto.Note is { Length: > DiagnosticRules.MaxAttachmentNoteLength })
            throw Violation(BusinessViolationCodes.ClinicalTextTooLong, "The note is too long.", nameof(dto.Note));

        _attachments.Add(new DiagnosticAttachment(dto.AttachmentId, dto.FileId, dto.ResultDate, dto.Note, dto.AttachedBy, dto.Now));

        SetDefaultValue(dto.UserContext);
        AddDomainEvent(new DiagnosticFileAttachedEvent
        {
            DiagnosticOrderId = Id, AttachmentId = dto.AttachmentId, FileId = dto.FileId,
            CorrelationId = dto.CorrelationId, UserContext = dto.UserContext
        });
        UpdateVersion();
    }

    /// <summary>Only a pending order with no results and no files may be cancelled -- anything that has
    /// come back is part of the record.</summary>
    public void Cancel(DiagnosticCancelDto dto)
    {
        EnsureNotCancelled();

        if (Status != DiagnosticOrderStatus.Pending || _resultSets.Count > 0 || _attachments.Count > 0)
            throw Violation(BusinessViolationCodes.DiagnosticOrderHasResults, ClinicalMessages.DiagnosticOrderHasResults, nameof(Status));

        if (string.IsNullOrWhiteSpace(dto.Reason) || dto.Reason.Length > DiagnosticRules.MaxCancelReasonLength)
            throw Violation(BusinessViolationCodes.ClinicalTextTooLong, $"Give a reason of up to {DiagnosticRules.MaxCancelReasonLength} characters.", nameof(dto.Reason));

        Status = DiagnosticOrderStatus.Cancelled;
        CancelledReason = dto.Reason;
        CancelledOn = dto.Now;
        CancelledBy = dto.CancelledBy;

        SetDefaultValue(dto.UserContext);
        AddDomainEvent(new DiagnosticOrderCancelledEvent
        {
            DiagnosticOrderId = Id, VisitId = VisitId, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext
        });
        UpdateVersion();
    }

    private DiagnosticResultValue ToValue(ResultValueDto value)
    {
        if (string.IsNullOrWhiteSpace(value.Name) || value.Name.Length > DiagnosticRules.MaxValueNameLength)
            throw Violation(BusinessViolationCodes.InvalidDiagnosticResult, $"Each value needs a name of up to {DiagnosticRules.MaxValueNameLength} characters.", nameof(value.Name));

        if (value.Unit is { Length: > DiagnosticRules.MaxUnitLength } || value.TextValue is { Length: > DiagnosticRules.MaxTextValueLength }
            || value.NormalText is { Length: > DiagnosticRules.MaxNormalTextLength })
            throw Violation(BusinessViolationCodes.InvalidDiagnosticResult, $"The value '{value.Name}' has a field that is too long.", nameof(value.Name));

        if (value.ReferenceLow is { } low && value.ReferenceHigh is { } high && low > high)
            throw Violation(BusinessViolationCodes.InvalidReferenceRange, $"The reference range for '{value.Name}' is back to front: the low bound is above the high bound.", nameof(value.ReferenceLow));

        ResultFlag flag;
        if (value.ValueKind == ResultValueKind.Numeric)
        {
            if (value.NumericValue is null || string.IsNullOrWhiteSpace(value.Unit))
                throw Violation(BusinessViolationCodes.InvalidDiagnosticResult, $"A numeric value for '{value.Name}' needs a number and a unit.", nameof(value.NumericValue));

            flag = ReferenceRangeClassifier.ClassifyNumeric(value.NumericValue.Value, value.ReferenceLow, value.ReferenceHigh);
            return new DiagnosticResultValue(
                value.ValueId, value.AnalyteId, value.Name, value.ValueKind, value.NumericValue, null, value.Unit,
                value.ReferenceLow, value.ReferenceHigh, null, flag);
        }

        if (string.IsNullOrWhiteSpace(value.TextValue))
            throw Violation(BusinessViolationCodes.InvalidDiagnosticResult, $"A qualitative value for '{value.Name}' needs a result.", nameof(value.TextValue));

        flag = ReferenceRangeClassifier.ClassifyQualitative(value.TextValue, value.NormalText);
        return new DiagnosticResultValue(
            value.ValueId, value.AnalyteId, value.Name, value.ValueKind, null, value.TextValue, value.Unit,
            null, null, string.IsNullOrWhiteSpace(value.NormalText) ? null : value.NormalText, flag);
    }

    private void EnsureNotCancelled()
    {
        if (Status == DiagnosticOrderStatus.Cancelled)
            throw Violation(BusinessViolationCodes.DiagnosticOrderCancelled, "This order was cancelled.", nameof(Status));
    }

    private static DomainRuleViolationException Violation(string code, string message, string property) => new(code, message, property);
}
