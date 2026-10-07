namespace CatEcommerce.Infrastructure.Security.Captcha;

/// <summary>HO-ACC-001 §12.6. The answer never leaves this layer -- only the id and the rendered image do.</summary>
public sealed record CaptchaChallenge(string CaptchaId, byte[] ImageBytes, int ExpiresInSeconds);
