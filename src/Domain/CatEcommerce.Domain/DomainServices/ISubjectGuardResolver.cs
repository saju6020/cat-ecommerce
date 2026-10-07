using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-NTF-001 §8.7/§8.13 -- resolves the one <see cref="INotificationSubjectGuard"/> for a subject
/// kind. Added beyond §8.13's own file list (recorded as a deviation in the implementation report)
/// so the Application-layer command handlers that need it (C4, C5) depend on a Domain interface
/// rather than reaching into Infrastructure's own <c>SubjectGuardResolver</c> concrete type, which
/// would invert the layering `.ai/architecture.md` requires.
/// </summary>
public interface ISubjectGuardResolver
{
    Task<bool> IsStillValidAsync(NotificationSubjectKind? kind, Guid recipientId, Guid? subjectId, Guid? subjectRef, DateOnly? subjectDueOn, string? subjectStamp = null, CancellationToken ct = default);
}
