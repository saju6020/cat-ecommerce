namespace CatEcommerce.Api.Contracts.Operations;

/// <summary>HO-XCT-001 section 12.4 -- <c>POST /api/admin/customers/{customerAccountId}/erasure</c>. The customer id is in the route.</summary>
public sealed class EraseCustomerRequest
{
    /// <summary>InPerson, Phone, Email or Letter.</summary>
    public string? Channel { get; init; }
    /// <summary>A fixed code: RegisteredMobileCallback, RegisteredEmailReply, InPersonPhotoId or WrittenRequestSigned. Never free text.</summary>
    public string? IdentityCheck { get; init; }
}

/// <summary>HO-XCT-001 section 12.4 -- <c>POST /api/admin/retention/erasures/reapply</c>. Ids come from the erasure journal exported with the backup.</summary>
public sealed class ReapplyErasuresRequest
{
    public IReadOnlyList<Guid>? CustomerAccountIds { get; init; }
    public string? RestoreReference { get; init; }
}
