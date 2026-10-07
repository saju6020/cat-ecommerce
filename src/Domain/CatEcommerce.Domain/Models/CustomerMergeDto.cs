using CatEcommerce.Domain.ValueObjects;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

/// <summary>HO-ACC-001 §8.9 (MergeInto). Carries the survivor's own identifiers so the duplicate
/// can apply the match rule without a second aggregate load inside itself.
///
/// CODE_REVIEW gate, MEDIUM (2026-09-25): <see cref="MergedOn"/> is threaded in from the caller,
/// the same way <c>CustomerAnonymisationDto.AnonymisedOn</c> already is -- <c>MergeInto</c> must
/// not read the wall clock itself.</summary>
public sealed record CustomerMergeDto(
    Guid SurvivingCustomerId,
    string Reason,
    MobileNumber? SurvivorMobile,
    IReadOnlyList<string> SurvivorAdditionalPhoneNumbers,
    EmailAddress? SurvivorEmail,
    DateTimeOffset MergedOn,
    UserContext UserContext,
    Guid CorrelationId);
