using CatEcommerce.Domain.Aggregates.Notification;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Constants;

namespace CatEcommerce.Infrastructure.Notifications;

/// <summary>
/// HO-NTF-001 §8.4 (ADR-056/057) -- replaces the contracts-pass <c>InMemoryNotificationOutbox</c>
/// (docs/handover/CONTRACTS.md #16). Builds the request aggregate and adds it to the scoped
/// <see cref="CatEcommerceDbContext"/> <b>without saving</b>: the producer's own repository save
/// commits it with the business change (F23). Never call <c>SaveChangesAsync</c> here.
/// </summary>
public sealed class EfNotificationOutbox(CatEcommerceDbContext db, INotificationTemplateCatalogue catalogue, TimeProvider timeProvider) : INotificationOutbox
{
    public void Enqueue(NotificationRequestDto request)
    {
        var definition = catalogue.Find(request.TemplateKey);

        var aggregate = new NotificationRequestAggregate(request.RequestId);
        aggregate.Create(new NotificationRequestCreateDto(
            request.TemplateKey,
            request.RecipientKind,
            request.RecipientId,
            request.Variables,
            request.ChannelHint,
            request.SubjectKind,
            request.SubjectId,
            request.SubjectRef,
            request.SubjectDueOn,
            request.BranchId,
            request.DeduplicationKey,
            request.NotAfter,
            ContainsSecret: definition?.ContainsSecret ?? false,
            RequestedOn: timeProvider.GetUtcNow(),
            RequestedBy: request.UserContext?.UserId ?? SystemActors.NotificationWorker,
            UserContext: request.UserContext ?? new Platform.Infrastructure.Common.Security.UserContext(SystemActors.NotificationWorker, Guid.Empty),
            CorrelationId: request.CorrelationId == default ? Guid.NewGuid() : request.CorrelationId,
            SendAfter: request.SendAfter,
            SubjectStamp: request.SubjectStamp));

        db.NotificationRequests.Add(aggregate);
    }
}
