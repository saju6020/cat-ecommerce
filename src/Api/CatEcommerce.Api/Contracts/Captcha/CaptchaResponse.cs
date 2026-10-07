namespace CatEcommerce.Api.Contracts.Captcha;

public sealed record CaptchaResponse(string CaptchaId, string ImageDataUrl, int ExpiresInSeconds);
