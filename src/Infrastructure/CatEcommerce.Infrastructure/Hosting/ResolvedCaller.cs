namespace CatEcommerce.Infrastructure.Hosting;

public enum ResolvedCallerKind
{
    Anonymous,
    Customer,
    Staff
}

/// <summary>
/// HO-ACC-001 §12.5 / ADR-032. Written into <c>HttpContext.Items</c> by
/// <c>SessionAuthenticationMiddleware</c> (Api), read by <see cref="SessionUserContextProvider"/>
/// (this class lives in Infrastructure, not Api, because <see cref="SessionUserContextProvider"/>
/// -- which Api's DI registers as <c>IUserContextProvider</c> -- needs a type both layers can
/// share; Api already references Infrastructure).
/// </summary>
public sealed record ResolvedCaller
{
    /// <summary>The single shared key both the middleware and the provider use for
    /// <c>HttpContext.Items</c>. A <c>static readonly object</c>, not a string, so no other
    /// middleware can collide with it by using the same string literal.</summary>
    public static readonly object HttpContextItemsKey = new();

    public ResolvedCallerKind Kind { get; }
    public Guid AccountId { get; }
    public IReadOnlyList<string> RoleIds { get; }

    private ResolvedCaller(ResolvedCallerKind kind, Guid accountId, IReadOnlyList<string> roleIds)
    {
        Kind = kind;
        AccountId = accountId;
        RoleIds = roleIds;
    }

    public static ResolvedCaller Anonymous { get; } = new(ResolvedCallerKind.Anonymous, Guid.Empty, []);

    public static ResolvedCaller Customer(Guid customerAccountId) =>
        new(ResolvedCallerKind.Customer, customerAccountId, ["Customer"]);

    public static ResolvedCaller Staff(Guid staffAccountId, IReadOnlyList<string> roleIds) =>
        new(ResolvedCallerKind.Staff, staffAccountId, roleIds);
}
