namespace CatEcommerce.Read.ViewModels;

/// <summary>HO-XCT-001 section 12.4 Q6. The ledger holds no name, phone or email (they no longer exist), so neither does this view.</summary>
public sealed record ErasureScopeViewModel(
    string ContextKey, string Status, DateOnly? HeldUntil, string? Basis, int Attempts, string? LastErrorCode, DateTimeOffset? CompletedOn);

public sealed record ErasureRequestViewModel(
    Guid CustomerAccountId, DateTimeOffset RequestedOn, string Source, string Status, DateTimeOffset? CompletedOn,
    IReadOnlyList<ErasureScopeViewModel> Scopes);
