namespace CatEcommerce.Read.ViewModels;

public sealed record NotificationLogItemViewModel(
    Guid Id, DateTimeOffset CreatedOn, string TemplateKey, string TemplateName, string Category, string Channel,
    string? DestinationMasked, string RecipientKind, Guid RecipientId, string RecipientName, string Status,
    int AttemptCount, DateTimeOffset? SentOn, string? LastProviderResponse, DateTimeOffset? DeferredUntil, bool IsResend, bool IsFallback);

public sealed record NotificationAttemptViewModel(DateTimeOffset AttemptedOn, string Outcome, string ProviderName, string? ProviderMessageId, string? ProviderResponse);

public sealed record NotificationChainViewModel(Guid RequestId, Guid? FallbackFromId, IReadOnlyList<Guid> Fallbacks, Guid? ResendOfId, IReadOnlyList<Guid> Resends);

public sealed record NotificationDetailViewModel(
    Guid Id, DateTimeOffset CreatedOn, string TemplateKey, string TemplateName, string Category, string Channel,
    string? Destination, string RecipientKind, Guid RecipientId, string RecipientName, string Status,
    int AttemptCount, DateTimeOffset? SentOn, DateTimeOffset? DeferredUntil, bool IsResend, bool IsFallback,
    string? Subject, string? Title, string Body, string? RedactedReason,
    IReadOnlyList<NotificationAttemptViewModel> Attempts, NotificationChainViewModel Chain,
    bool CanResend, string? CannotResendReason, IReadOnlyList<string> ResendableChannels);

public sealed record NotificationLast24HoursViewModel(int Sent, int Undeliverable, int Expired, int Cancelled, int Suppressed);

public sealed record NotificationChannelsConfiguredViewModel(bool Sms, bool Email);

public sealed record NotificationSummaryViewModel(
    int Queued, int RetryPending, int Sending, DateTimeOffset? OldestQueuedOn,
    NotificationLast24HoursViewModel Last24h, NotificationChannelsConfiguredViewModel ChannelsConfigured);

public sealed record InboxItemViewModel(Guid Id, string? Title, string Body, string? LinkPath, string Category, DateTimeOffset CreatedOn, DateTimeOffset? ReadOn);
