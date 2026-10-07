namespace CatEcommerce.Read.ViewModels;

public sealed record NotificationTemplateSummaryViewModel(
    string Key, string DisplayName, string Description, string Category, string Urgency,
    IReadOnlyList<string> Channels, bool ContainsSecret, bool IsCustomised, int ContentVersion, DateTimeOffset? LastEditedOn);

public sealed record NotificationTemplateVariableViewModel(
    string Name, string Description, string Kind, string Sample, IReadOnlyList<string> RequiredOn, bool IsStandard);

public sealed record NotificationTemplateContentViewModel(
    string Channel, string? Subject, string? Title, string Body, string? LinkPath);

public sealed record NotificationTemplateChannelLimitsViewModel(string Channel, int? SubjectMax, int? TitleMax, int BodyMax);

public sealed record NotificationTemplateDetailViewModel(
    string Key, string DisplayName, string Description, string Category, string Urgency,
    IReadOnlyList<string> Channels, bool ContainsSecret, bool IsCustomised, int ContentVersion, DateTimeOffset? LastEditedOn,
    IReadOnlyList<NotificationTemplateVariableViewModel> Variables,
    IReadOnlyList<NotificationTemplateContentViewModel> Contents,
    IReadOnlyList<NotificationTemplateContentViewModel> Defaults,
    IReadOnlyList<NotificationTemplateChannelLimitsViewModel> Limits);

public sealed record NotificationSmsMeasureViewModel(string Encoding, int Characters, int Segments);

public sealed record NotificationTemplatePreviewChannelViewModel(
    string Channel, string? Subject, string? Title, string TextBody, string? HtmlBody, string? LinkPath, NotificationSmsMeasureViewModel? Sms);

/// <summary>HO-NTF-001 §12.4 -- "Validation errors are the same 400s as C1." <see cref="ErrorCode"/>
/// null means success; the controller maps a non-null one to the C1 <c>ValidationResponse</c>
/// shape (queries have no <c>ValidationResult</c> of their own to carry it in).</summary>
public sealed record NotificationTemplatePreviewViewModel(
    IReadOnlyList<NotificationTemplatePreviewChannelViewModel> Channels,
    string? ErrorCode = null, string? ErrorMessage = null, string? ErrorProperty = null);
