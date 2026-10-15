namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-POR-001 §10.3. Plain language, no ids (NFR-USA-4). There are no per-<c>ReorderLineOutcome</c> messages: re-order is retired (ADR-263).</summary>
public static class PortalMessages
{
    /// <summary>ADR-264: there is no pay-online action; this is guidance only (SRS §1.2).</summary>
    public const string HowToPayOffline =
        "You can pay at the clinic by cash or card, or by bank transfer. Contact the clinic if you need the transfer details.";

    /// <summary>The one log template of the dashboard's only catch site (ADR-260, I-X1): section name, exception, correlation id; no customer data.</summary>
    public const string SectionUnavailableLogTemplate = "Portal section {Section} unavailable. CorrelationId={CorrelationId}";
}
