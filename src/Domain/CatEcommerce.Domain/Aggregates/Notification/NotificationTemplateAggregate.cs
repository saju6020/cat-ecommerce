using CatEcommerce.Domain.Entities;
using CatEcommerce.Domain.Events.Notification;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Helpers;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Notification;

/// <summary>
/// HO-NTF-001 §8.2 -- exists only once a template is customised. <c>Key</c> must be in the
/// catalogue; <c>Contents</c> is exactly one entry per channel of the definition.
/// </summary>
public sealed class NotificationTemplateAggregate : AggregateRoot
{
    private readonly List<NotificationTemplateContent> _contents = [];

    public string Key { get; private set; } = default!;
    public string Locale { get; private set; } = "en";
    public IReadOnlyList<NotificationTemplateContent> Contents => _contents;
    public int ContentVersion { get; private set; }
    public Guid LastEditedBy { get; private set; }
    public DateTimeOffset LastEditedOn { get; private set; }

    private NotificationTemplateAggregate() { }

    public NotificationTemplateAggregate(Guid id) : base(id) { }

    public void Customise(NotificationTemplateCustomiseDto dto)
    {
        Key = dto.Definition.Key;
        Apply(dto);

        AddDomainEvent(new NotificationTemplateCustomisedEvent
        {
            Key = Key,
            ContentVersion = ContentVersion,
            CorrelationId = dto.CorrelationId,
            UserContext = dto.UserContext
        });

        UpdateVersion();
    }

    public void Edit(NotificationTemplateCustomiseDto dto)
    {
        Apply(dto);

        AddDomainEvent(new NotificationTemplateCustomisedEvent
        {
            Key = Key,
            ContentVersion = ContentVersion,
            CorrelationId = dto.CorrelationId,
            UserContext = dto.UserContext
        });

        UpdateVersion();
    }

    public void ResetToDefault(Guid actorId, DateTimeOffset asOf, Platform.Infrastructure.Common.Security.UserContext userContext, Guid correlationId)
    {
        _contents.Clear();
        ContentVersion++;
        LastEditedBy = actorId;
        LastEditedOn = asOf;

        AddDomainEvent(new NotificationTemplateResetEvent
        {
            Key = Key,
            ContentVersion = ContentVersion,
            CorrelationId = correlationId,
            UserContext = userContext
        });

        UpdateVersion();
    }

    private void Apply(NotificationTemplateCustomiseDto dto)
    {
        var violation = TemplateRules.Validate(dto.Definition, dto.Contents);
        if (violation is not null)
            throw new DomainRuleViolationException(violation.Code, violation.Message, violation.PropertyName);

        _contents.Clear();
        foreach (var content in dto.Contents)
            _contents.Add(new NotificationTemplateContent(Guid.NewGuid(), content.Channel, content.Subject, content.Title, content.Body, content.LinkPath));

        ContentVersion++;
        LastEditedBy = dto.EditedBy;
        LastEditedOn = dto.EditedOn;
    }
}
