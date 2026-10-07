using System.Security.Cryptography;
using System.Text;
using CatEcommerce.Infrastructure.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Base64UrlHelper = CatEcommerce.Infrastructure.Security.Base64Url;

namespace CatEcommerce.Infrastructure.Security.Captcha;

/// <summary>
/// HO-ACC-001 §12.6 / ADR-029. Singleton, <b>owns</b> its own <see cref="MemoryCache"/> (never the
/// shared <c>IMemoryCache</c>) so <see cref="MemoryCacheOptions.SizeLimit"/> bounds it -- a
/// noisy-neighbour cannot starve it, and it cannot starve anything else.
/// </summary>
public sealed class MemoryCacheCaptchaService : ICaptchaService, IDisposable
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no 0/O, 1/I/L

    private readonly MemoryCache _cache;
    private readonly ICaptchaImageRenderer _renderer;
    private readonly CaptchaOptions _options;

    // SECURITY gate LOW: TryGetValue followed by Remove is not atomic on its own -- concurrent
    // submits of one solved challenge could all TryGetValue the same answer before any of them
    // reached Remove. This lock makes the read-and-consume one atomic step, so a challenge really
    // is single-use even under a parallel burst.
    private readonly object _cacheLock = new();

    public MemoryCacheCaptchaService(IOptions<CaptchaOptions> options, ICaptchaImageRenderer renderer)
    {
        _options = options.Value;
        _renderer = renderer;
        _cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = _options.MaxOutstandingChallenges });
    }

    public CaptchaChallenge Issue()
    {
        var id = Base64UrlHelper.Encode(RandomNumberGenerator.GetBytes(16));
        var answer = string.IsNullOrEmpty(_options.TestAnswer) ? GenerateAnswer(_options.Length) : _options.TestAnswer;
        var imageBytes = _renderer.Render(answer);

        _cache.Set(id, answer, new MemoryCacheEntryOptions
        {
            Size = 1,
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(_options.TtlSeconds)
        });

        return new CaptchaChallenge(id, imageBytes, _options.TtlSeconds);
    }

    public bool Verify(string? captchaId, string? answer)
    {
        if (string.IsNullOrWhiteSpace(captchaId) || string.IsNullOrWhiteSpace(answer)) return false;

        // Get-and-remove as ONE atomic step under the lock -- removed unconditionally, before
        // comparing, so one challenge buys exactly one guess even under concurrent submits of the
        // same solved challenge (SECURITY gate LOW).
        string? expected;
        lock (_cacheLock)
        {
            if (!_cache.TryGetValue(captchaId, out expected))
                expected = null;

            _cache.Remove(captchaId);
        }

        if (expected is null) return false;

        return FixedTimeEqualsIgnoreCase(answer.Trim(), expected.Trim());
    }

    private static string GenerateAnswer(int length)
    {
        var chars = new char[length];
        for (var i = 0; i < length; i++)
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];

        return new string(chars);
    }

    private static bool FixedTimeEqualsIgnoreCase(string a, string b)
    {
        var upperA = a.ToUpperInvariant();
        var upperB = b.ToUpperInvariant();

        const int bufferLength = 32;
        var bufferA = new byte[bufferLength];
        var bufferB = new byte[bufferLength];
        Encoding.UTF8.GetBytes(upperA, 0, Math.Min(upperA.Length, bufferLength), bufferA, 0);
        Encoding.UTF8.GetBytes(upperB, 0, Math.Min(upperB.Length, bufferLength), bufferB, 0);

        var lengthEqual = upperA.Length == upperB.Length;
        var contentEqual = CryptographicOperations.FixedTimeEquals(bufferA, bufferB);

        return lengthEqual & contentEqual; // bitwise & -- no short-circuit on the timing-sensitive path
    }

    public void Dispose() => _cache.Dispose();
}
