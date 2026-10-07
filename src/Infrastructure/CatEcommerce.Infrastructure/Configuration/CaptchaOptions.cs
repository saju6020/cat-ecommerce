using System.ComponentModel.DataAnnotations;

namespace CatEcommerce.Infrastructure.Configuration;

/// <summary>Bound from appsettings.json section "Captcha" -- HO-ACC-001 §12.6.</summary>
public sealed class CaptchaOptions
{
    public const string SectionName = "Captcha";

    [Range(4, 8)]
    public int Length { get; set; } = 5;

    public int TtlSeconds { get; set; } = 120;

    public int MaxOutstandingChallenges { get; set; } = 10000;

    /// <summary>
    /// Test seam (§12.6). Honoured only in Development/Testing -- the host refuses to start if
    /// this is set anywhere else (§20.9, I-A2). Never present in the committed appsettings.json.
    /// </summary>
    public string? TestAnswer { get; set; }
}
