using System.Text.RegularExpressions;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Order;

/// <summary>
/// HO-ORD-001 §8.14 / ADR-209, US-ORD-11 -- a courier the shop dispatches with. <b>No courier API exists</b> (SRS Q-4): this master record is
/// what a dispatch names, and its optional tracking-URL template (https, containing <c>{tracking}</c>) is what the customer's order page turns a
/// tracking number into a link with. <c>SupportsCod</c> tells staff whether the courier collects cash on delivery. The name is unique
/// (the handler's check, and a unique index).
/// </summary>
public sealed partial class CourierAggregate : AggregateRoot
{
    public const string TrackingPlaceholder = "{tracking}";

    public string Name { get; private set; } = default!;
    public string? TrackingUrlTemplate { get; private set; }
    public bool SupportsCod { get; private set; }
    public bool IsActive { get; private set; }

    private CourierAggregate() { }

    public CourierAggregate(Guid id) : base(id) { }

    public void Create(string name, string? trackingUrlTemplate, bool supportsCod, bool isActive, UserContext userContext)
    {
        Apply(name, trackingUrlTemplate, supportsCod, isActive);
        SetDefaultValue(userContext);
        UpdateVersion();
    }

    public void Update(string name, string? trackingUrlTemplate, bool supportsCod, bool isActive)
    {
        Apply(name, trackingUrlTemplate, supportsCod, isActive);
        UpdateVersion();
    }

    /// <summary>The courier's tracking link for a number, or <c>null</c> when the courier has no template or the number is blank. The number is
    /// URL-encoded, so it can never break out of the template.</summary>
    public string? BuildTrackingUrl(string? trackingNumber) =>
        TrackingUrlTemplate is null || string.IsNullOrWhiteSpace(trackingNumber)
            ? null
            : TrackingUrlTemplate.Replace(TrackingPlaceholder, Uri.EscapeDataString(trackingNumber.Trim()), StringComparison.Ordinal);

    private void Apply(string name, string? trackingUrlTemplate, bool supportsCod, bool isActive)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > OrderRules.MaxNameLength)
            throw Invalid($"A courier name is required (up to {OrderRules.MaxNameLength} characters).", nameof(Name));

        var template = string.IsNullOrWhiteSpace(trackingUrlTemplate) ? null : trackingUrlTemplate.Trim();
        if (template is not null) ValidateTemplate(template);

        Name = name.Trim();
        TrackingUrlTemplate = template;
        SupportsCod = supportsCod;
        IsActive = isActive;
    }

    private static void ValidateTemplate(string template)
    {
        if (template.Length > 500)
            throw Invalid("The tracking link template is too long.", nameof(TrackingUrlTemplate));

        if (!template.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            throw Invalid("The tracking link template must start with https://.", nameof(TrackingUrlTemplate));

        if (template.Split(TrackingPlaceholder).Length != 2)
            throw Invalid($"The tracking link template must contain {TrackingPlaceholder} exactly once.", nameof(TrackingUrlTemplate));

        // The template with a sample number must be a well-formed absolute https URL, with no credentials (user:pass@host) and no control characters.
        var probe = template.Replace(TrackingPlaceholder, "TRACK123", StringComparison.Ordinal);
        if (!Uri.TryCreate(probe, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo) || ControlCharacter().IsMatch(template))
            throw Invalid("The tracking link template is not a valid https address.", nameof(TrackingUrlTemplate));
    }

    private static DomainRuleViolationException Invalid(string message, string property) =>
        new(BusinessViolationCodes.InvalidCourier, message, property);

    [GeneratedRegex(@"[\p{Cc}\s]")]
    private static partial Regex ControlCharacter();
}
