using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Read.Queries.Notification;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Notification;

/// <summary>HO-NTF-001 §8.2/§12.6 -- pure, no repository. Renders unsaved content with the
/// definition's samples (or the caller's overrides); writes nothing.</summary>
public sealed class PreviewNotificationTemplateQueryHandler(INotificationTemplateCatalogue catalogue)
    : IQueryHandlerAsync<PreviewNotificationTemplateQuery, NotificationTemplatePreviewViewModel>
{
    public Task<QueryResponse<NotificationTemplatePreviewViewModel>> HandleAsync(PreviewNotificationTemplateQuery query)
    {
        var definition = catalogue.Find(query.Key);
        if (definition is null)
            return Task.FromResult(Error(BusinessViolationCodes.TemplateNotInCatalogue, "Key", $"'{query.Key}' is not a known template."));

        // Gate fix 2026-09-27 (SECURITY LOW): a null entry in Contents, or an unrecognised channel
        // name, used to reach Enumeration.FromDisplayName / a null-reference unguarded and surface
        // as an unhandled 500. Both are now the same 400 the rest of this handler already returns.
        var contents = new List<TemplateContentInput>();
        foreach (var item in query.Contents)
        {
            if (item is null)
                return Task.FromResult(Error(BusinessViolationCodes.InvalidTemplateContents, "Contents", "Contents must not contain a null entry."));

            if (!EnumerationParsing.TryParse<NotificationChannel>(item.Channel, out var parsedChannel) || parsedChannel is null)
                return Task.FromResult(Error(BusinessViolationCodes.InvalidTemplateChannel, "Contents", $"'{item.Channel}' is not a known channel."));

            contents.Add(new TemplateContentInput(parsedChannel, item.Subject, item.Title, item.Body, item.LinkPath));
        }

        var violation = TemplateRules.Validate(definition, contents);
        if (violation is not null)
            return Task.FromResult(Error(violation.Code, violation.PropertyName, violation.Message));

        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [StandardTemplateVariables.RecipientName] = "Jane Doe",
            [StandardTemplateVariables.ClinicName] = "PurrCare Main Clinic",
            [StandardTemplateVariables.ClinicPhone] = "+8801700000000"
        };

        foreach (var variable in definition.Variables)
        {
            var overrideValue = query.SampleValues is not null && query.SampleValues.TryGetValue(variable.Name, out var sample) ? sample : null;

            // Gate fix 2026-09-27 (SECURITY LOW): a non-https Url override, a malformed Date
            // override, or one carrying a control/private-use character used to throw inside
            // NotificationRenderer (InvalidOperationException / FormatException) rather than
            // return 400.
            if (overrideValue is not null)
            {
                var sampleViolation = TemplateRules.ValidateSampleOverride(variable.Name, variable.Kind, overrideValue, $"SampleValues.{variable.Name}");
                if (sampleViolation is not null)
                    return Task.FromResult(Error(sampleViolation.Code, sampleViolation.PropertyName, sampleViolation.Message));
            }

            values[variable.Name] = overrideValue ?? variable.Sample;
        }

        if (definition.Category == NotificationCategory.Marketing)
            values[StandardTemplateVariables.UnsubscribeLink] = "https://portal.example.com/unsubscribe#token=sample";

        var channels = new List<NotificationTemplatePreviewChannelViewModel>();
        foreach (var content in contents)
        {
            var rendered = NotificationRenderer.Render(
                new NotificationTemplateChannelDefault(content.Channel, content.Subject, content.Title, content.Body, content.LinkPath),
                definition.Variables, values, content.Channel);

            var sms = content.Channel == NotificationChannel.Sms ? SmsSegments.Measure(rendered.TextBody) : ((SmsEncoding, int, int)?)null;

            channels.Add(new NotificationTemplatePreviewChannelViewModel(
                content.Channel.DisplayName, rendered.Subject, rendered.Title, rendered.TextBody, rendered.HtmlBody, rendered.LinkPath,
                sms is null ? null : new NotificationSmsMeasureViewModel(sms.Value.Item1.DisplayName, sms.Value.Item2, sms.Value.Item3)));
        }

        return Task.FromResult(new QueryResponse<NotificationTemplatePreviewViewModel> { Result = new NotificationTemplatePreviewViewModel(channels) });
    }

    private static QueryResponse<NotificationTemplatePreviewViewModel> Error(string code, string property, string message) =>
        new() { Result = new NotificationTemplatePreviewViewModel([], code, message, property) };
}
