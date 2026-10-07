namespace CatEcommerce.Api.Contracts;

/// <summary>HO-ACC-001 §12.6 / ADR-029.</summary>
public interface ICaptchaProtectedRequest
{
    string? CaptchaId { get; }
    string? CaptchaAnswer { get; }
}
