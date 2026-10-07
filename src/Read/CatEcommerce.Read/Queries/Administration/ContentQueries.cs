using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Administration;

public sealed class GetAdminContentQuery : Query<IReadOnlyList<ContentItemViewModel>>
{
    public string? Kind { get; init; }
}

public sealed class GetAdminContentItemQuery : Query<ContentItemViewModel?>
{
    public required Guid ContentItemId { get; init; }
}

public sealed class GetPublishedContentQuery : Query<PublishedContentViewModel?>
{
    public required string Kind { get; init; }
    public required string Slug { get; init; }
}

public sealed class GetPublishedBannersQuery : Query<IReadOnlyList<PublishedContentViewModel>>
{
    public string? Placement { get; init; }
}

public sealed class GetPublishedFaqsQuery : Query<IReadOnlyList<PublishedContentViewModel>>;

public sealed class GetAdminPoliciesQuery : Query<IReadOnlyList<PolicyDocumentViewModel>>;

public sealed class GetPublicPolicyQuery : Query<PolicyVersionViewModel?>
{
    public required string Key { get; init; }
}

public sealed class GetPendingPolicyAcceptancesQuery : Query<IReadOnlyList<PolicyVersionViewModel>>
{
    public required Guid CustomerAccountId { get; init; }
}

public sealed class GetSystemStatusQuery : Query<SystemStatusViewModel>;
