using CatEcommerce.Application.Commands.Notification;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Application.DataMappers;

/// <summary>HO-NTF-001 §11.3. Static <c>ToDomainDto()</c> extensions; <c>UserContext</c>/
/// <c>CorrelationId</c> passed through from the command. Every enum-typed field is parsed with
/// <see cref="EnumerationParsing.TryParse{T}"/> (case-insensitive) rather than
/// <see cref="Enumeration.FromDisplayName{T}"/> directly, matching <c>AdministrationMapper</c>'s
/// convention -- the command's own validator has already rejected an unrecognised value.</summary>
public static class NotificationMapper
{
    public static NotificationRequestDto ToDomainDto(this RequestNotificationCommand c) =>
        new(
            c.RequestId, c.TemplateKey,
            ParseEnum<NotificationRecipientKind>(c.RecipientKind), c.RecipientId,
            c.Variables,
            ParseNullableEnum<NotificationChannel>(c.ChannelHint),
            ParseNullableEnum<NotificationSubjectKind>(c.SubjectKind),
            c.SubjectId, c.SubjectRef, c.SubjectDueOn,
            c.BranchId, c.DeduplicationKey, c.NotAfter,
            c.CorrelationId, c.UserContext);

    private static T ParseEnum<T>(string value) where T : Enumeration
    {
        if (!EnumerationParsing.TryParse<T>(value, out var result) || result is null)
            throw new InvalidOperationException($"'{value}' is not a recognised {typeof(T).Name}. This should have been rejected by the command's own validator.");

        return result;
    }

    private static T? ParseNullableEnum<T>(string? value) where T : Enumeration =>
        string.IsNullOrWhiteSpace(value) ? null : ParseEnum<T>(value);
}
