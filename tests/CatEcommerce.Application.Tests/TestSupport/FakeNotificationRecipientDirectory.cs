using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Application.Tests.TestSupport;

/// <summary>
/// Hand-written per HO-CAT-001 §13 (no mocking library). Resolves whatever <see cref="RecipientProfile"/>
/// was seeded for a given (kind, id) pair -- defaults to a profile with the SAME id (no merge), so a
/// test only needs to seed an entry when it specifically wants to exercise the merged-to-survivor
/// redirect (HO-NTF-001 CODE_REVIEW gate fix, 2026-09-27:
/// UnsubscribeFromMarketingCommandHandlerTests' merge case).
/// </summary>
public sealed class FakeNotificationRecipientDirectory : INotificationRecipientDirectory
{
    private readonly Dictionary<(NotificationRecipientKind, Guid), RecipientProfile> _profiles = [];
    private readonly Dictionary<Guid, IReadOnlyList<Guid>> _mergedDuplicates = [];

    public void Seed(NotificationRecipientKind kind, Guid id, RecipientProfile profile) => _profiles[(kind, id)] = profile;

    public void SeedMergedDuplicates(Guid survivorId, params Guid[] duplicateIds) => _mergedDuplicates[survivorId] = duplicateIds;

    public Task<RecipientProfile?> ResolveAsync(NotificationRecipientKind kind, Guid id, CancellationToken ct = default)
    {
        RecipientProfile? result = _profiles.TryGetValue((kind, id), out var profile)
            ? profile
            : new RecipientProfile(kind, id, true, "Test Recipient", "+8801700000000", "recipient@example.com", NotificationChannel.Sms, _ => true);
        return Task.FromResult<RecipientProfile?>(result);
    }

    public Task<IReadOnlyList<Guid>> FindMergedDuplicatesAsync(Guid survivorId, CancellationToken ct = default) =>
        Task.FromResult(_mergedDuplicates.TryGetValue(survivorId, out var duplicates) ? duplicates : (IReadOnlyList<Guid>)[]);
}
