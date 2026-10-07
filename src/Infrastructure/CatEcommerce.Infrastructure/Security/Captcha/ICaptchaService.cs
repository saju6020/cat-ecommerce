namespace CatEcommerce.Infrastructure.Security.Captcha;

/// <summary>
/// HO-ACC-001 §12.6 / ADR-029. Infrastructure-only -- not one of the six Domain ports (§8.10);
/// <c>CaptchaController</c> and <c>CaptchaValidationFilter</c> call it directly, a recorded
/// exception to "controllers inject the dispatcher and user context only" because a challenge is
/// not a domain command.
/// </summary>
public interface ICaptchaService
{
    CaptchaChallenge Issue();

    /// <summary>Looks up then removes unconditionally, before comparing -- one challenge buys one
    /// guess. Expired/unknown/blank all return <c>false</c> with no distinction.</summary>
    bool Verify(string? captchaId, string? answer);
}
