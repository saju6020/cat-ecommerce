using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

/// <summary>What a staff member entered when the recording failed (HO-ORD-001 §8.16): the method, the amount, the reference they typed and when the money
/// arrived. Nothing about the failure's cause beyond <see cref="ReconciliationIssueDto.ErrorSummary"/>.</summary>
public sealed record EnteredPaymentDetails(string Method, decimal Amount, string? Reference, DateTimeOffset? ReceivedOn);

/// <summary><c>ErrorSummary</c> is the exception <b>type</b> and a fixed message -- never the exception's own message, which may carry data (ADR-205).</summary>
public sealed record ReconciliationIssueDto(
    Guid IssueId, Guid? OrderId, ReconciliationIssueKind Kind, EnteredPaymentDetails Payment, string CurrencyCode, string ErrorSummary, Guid CorrelationId,
    Guid RaisedBy, DateTimeOffset RaisedOn, UserContext UserContext, Guid BranchId);
