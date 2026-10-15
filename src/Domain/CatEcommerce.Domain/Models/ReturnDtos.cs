using CatEcommerce.Domain.Entities;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

/// <summary>One requested line (HO-ORD-001 §8.10). <c>RequiresDefectConfirmation</c> comes from <c>ReturnEligibilityPolicy</c>.</summary>
public sealed record ReturnLineDto(
    Guid OrderLineId, int Quantity, ReturnReason Reason, ItemCondition Condition, string? CustomerNote, bool RequiresDefectConfirmation);

public sealed record ReturnRequestDto(
    Guid OrderId, Guid? CustomerId, ReturnKind Kind, IReadOnlyList<ReturnLineDto> Lines, Guid RequestedBy, DateTimeOffset RequestedOn, UserContext UserContext, Guid CorrelationId);

/// <summary>Staff's approval of one line: the defect they confirmed (BR-10) and why.</summary>
public sealed record DefectConfirmationDto(Guid OrderLineId, bool DefectConfirmed, string? DefectNote);

/// <summary>What came back for one line, with where each unit goes (the original batches).</summary>
public sealed record ReturnReceiptDto(Guid OrderLineId, int QuantityReceived, IReadOnlyList<ReturnReceipt> Receipts);

