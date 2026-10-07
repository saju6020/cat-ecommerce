namespace CatEcommerce.Read.ViewModels;

public sealed record ContentItemViewModel(
    Guid Id, string Kind, string Slug, string Title, string DraftBody, string? PublishedBody, DateTimeOffset? PublishedOn,
    string Status, string? Placement, string? LinkUrl, DateTimeOffset? VisibleFrom, DateTimeOffset? VisibleTo, int? SortOrder);

public sealed record PublishedContentViewModel(
    string Kind, string Slug, string Title, string Body, DateTimeOffset PublishedOn, string? Placement,
    string? LinkUrl, int? SortOrder);

public sealed record PolicyDocumentViewModel(
    Guid Id, string Key, string Title, bool RequiresAcceptance, int? CurrentVersionNumber,
    IReadOnlyList<PolicyVersionSummaryViewModel> Versions);

public sealed record PolicyVersionSummaryViewModel(int VersionNumber, DateTimeOffset PublishedOn, string ChangeSummary);

public sealed record PolicyVersionViewModel(string PolicyKey, int VersionNumber, string Body, DateTimeOffset PublishedOn);

public sealed record SystemStatusViewModel(bool MaintenanceMode, string? Message);
