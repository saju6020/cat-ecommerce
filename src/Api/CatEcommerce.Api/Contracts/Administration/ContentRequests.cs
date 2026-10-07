namespace CatEcommerce.Api.Contracts.Administration;

public sealed class CreateContentItemRequest
{
    public string Kind { get; init; } = default!;
    public string Slug { get; init; } = default!;
    public string Title { get; init; } = default!;
    public string DraftBody { get; init; } = default!;
    public string? Placement { get; init; }
    public string? LinkUrl { get; init; }
    public DateTimeOffset? VisibleFrom { get; init; }
    public DateTimeOffset? VisibleTo { get; init; }
    public int? SortOrder { get; init; }
}

public sealed class EditContentDraftRequest
{
    public string Title { get; init; } = default!;
    public string DraftBody { get; init; } = default!;
    public string? Placement { get; init; }
    public string? LinkUrl { get; init; }
    public DateTimeOffset? VisibleFrom { get; init; }
    public DateTimeOffset? VisibleTo { get; init; }
    public int? SortOrder { get; init; }
}

public sealed class CreatePolicyDocumentRequest
{
    public string Key { get; init; } = default!;
    public string Title { get; init; } = default!;
    public bool RequiresAcceptance { get; init; }
}

public sealed class PublishPolicyVersionRequest
{
    public string Body { get; init; } = default!;
    public string ChangeSummary { get; init; } = default!;
}

public sealed class AcceptPolicyVersionRequest
{
    public string PolicyKey { get; init; } = default!;
    public int VersionNumber { get; init; }
}

public sealed class SetMaintenanceModeRequest
{
    public bool MaintenanceMode { get; init; }
    public string? Message { get; init; }
}
