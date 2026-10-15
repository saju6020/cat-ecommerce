namespace CatEcommerce.Infrastructure.Configuration;

/// <summary>
/// HO-ORD-001 §8.13, §20.8 (ADR-209, NFR-SEC-7) -- the courier providers whose tracking webhooks are accepted, bound from configuration section "Couriers".
/// <c>Couriers:Webhooks:{provider}:Secret</c> is a secret: it lives only in the git-ignored <c>appsettings.Development.json</c> or the secret store, <b>never in a
/// committed file</b>. Production has no provider until a courier is chosen (SRS Q-4); an unregistered provider is <c>404</c>.
/// </summary>
public sealed class CouriersOptions
{
    public const string SectionName = "Couriers";

    public Dictionary<string, CourierWebhookOptions> Webhooks { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class CourierWebhookOptions
{
    /// <summary>The shared secret the provider signs bodies with (HMAC-SHA256). Never logged, never echoed.</summary>
    public string Secret { get; set; } = string.Empty;

    /// <summary>The header carrying the signature: lower-case hex of the HMAC of the raw body, optionally prefixed <c>sha256=</c>.</summary>
    public string SignatureHeader { get; set; } = "X-Signature";
}
