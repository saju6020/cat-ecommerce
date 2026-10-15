using CatEcommerce.Domain.Events.Visit;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Visit;

/// <summary>
/// HO-EMR-001 §8.4 -- a correction to a <i>finalised</i> visit. Append-only: <see cref="Add"/> is the
/// only public method (U-C9), the original note stays exactly as finalised, and the database refuses
/// any <c>UPDATE</c>/<c>DELETE</c>/<c>TRUNCATE</c> of the table (ADR-118). A wrong addendum is answered
/// by another addendum.
/// </summary>
public sealed class VisitAddendumAggregate : AggregateRoot
{
    public Guid VisitId { get; private set; }
    public Guid CatId { get; private set; }
    public string Text { get; private set; } = default!;
    public string? Reason { get; private set; }
    public Guid AuthorStaffId { get; private set; }
    public DateTimeOffset AddedOn { get; private set; }

    private VisitAddendumAggregate() { }

    public VisitAddendumAggregate(Guid id) : base(id) { }

    public void Add(VisitAddendumDto dto)
    {
        if (dto.VisitStatus != VisitStatus.Finalised)
            throw new DomainRuleViolationException(
                BusinessViolationCodes.AddendumRequiresFinalisedVisit, ClinicalMessages.AddendumRequiresFinalisedVisit, nameof(dto.VisitStatus));

        if (string.IsNullOrWhiteSpace(dto.Text) || dto.Text.Length > ClinicalNoteRules.MaxAddendumLength)
            throw new DomainRuleViolationException(
                BusinessViolationCodes.ClinicalTextTooLong, $"An addendum is between 1 and {ClinicalNoteRules.MaxAddendumLength} characters.", nameof(dto.Text));

        if (dto.Reason is { Length: > ClinicalNoteRules.MaxAddendumReasonLength })
            throw new DomainRuleViolationException(BusinessViolationCodes.ClinicalTextTooLong, "The reason is too long.", nameof(dto.Reason));

        VisitId = dto.VisitId;
        CatId = dto.CatId;
        Text = dto.Text;
        Reason = string.IsNullOrWhiteSpace(dto.Reason) ? null : dto.Reason;
        AuthorStaffId = dto.AuthorStaffId;
        AddedOn = dto.Now;

        SetDefaultValue(dto.UserContext);
        AddDomainEvent(new VisitAddendumAddedEvent
        {
            AddendumId = Id, VisitId = VisitId, CatId = CatId,
            CorrelationId = dto.CorrelationId, UserContext = dto.UserContext
        });
        UpdateVersion();
    }
}
