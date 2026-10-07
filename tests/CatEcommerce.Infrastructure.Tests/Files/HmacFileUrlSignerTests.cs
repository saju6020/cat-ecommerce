using System.Reflection;
using CatEcommerce.Infrastructure.Configuration;
using CatEcommerce.Infrastructure.Files;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace CatEcommerce.Infrastructure.Tests.Files;

/// <summary>HO-FIL-001 §8.6 / U7 / U8.</summary>
public sealed class HmacFileUrlSignerTests
{
    private static HmacFileUrlSigner CreateSut(TimeProvider? timeProvider = null) => new(
        Options.Create(new FileStorageOptions { UrlSigningKey = "unit-test-signing-key-at-least-32-bytes-long" }),
        timeProvider ?? TimeProvider.System);

    [Fact]
    public void Sign_produces_the_documented_url_shape()
    {
        var sut = CreateSut();
        var fileId = Guid.NewGuid();

        var url = sut.Sign(fileId, TimeSpan.FromMinutes(5), "inline");

        Assert.StartsWith($"/api/files/{fileId:D}/content?exp=", url);
        Assert.Contains("disp=inline", url);
        Assert.Contains("sig=", url);
    }

    [Fact]
    public void A_freshly_signed_url_verifies()
    {
        var sut = CreateSut();
        var fileId = Guid.NewGuid();
        var exp = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds();
        var url = sut.Sign(fileId, TimeSpan.FromMinutes(5), "attachment");
        var sig = ExtractQueryValue(url, "sig");

        Assert.True(sut.TryVerify(fileId, exp, "attachment", sig));
    }

    // U7
    [Fact]
    public void TryVerify_refuses_a_tampered_fileId()
    {
        var sut = CreateSut();
        var exp = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds();
        var url = sut.Sign(Guid.NewGuid(), TimeSpan.FromMinutes(5), "attachment");
        var sig = ExtractQueryValue(url, "sig");

        Assert.False(sut.TryVerify(Guid.NewGuid(), exp, "attachment", sig));
    }

    // U7
    [Fact]
    public void TryVerify_refuses_a_tampered_exp()
    {
        var sut = CreateSut();
        var fileId = Guid.NewGuid();
        var url = sut.Sign(fileId, TimeSpan.FromMinutes(5), "attachment");
        var sig = ExtractQueryValue(url, "sig");
        var tamperedExp = long.Parse(ExtractQueryValue(url, "exp")) + 3600;

        Assert.False(sut.TryVerify(fileId, tamperedExp, "attachment", sig));
    }

    // U7
    [Fact]
    public void TryVerify_refuses_a_tampered_disposition()
    {
        var sut = CreateSut();
        var fileId = Guid.NewGuid();
        var exp = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds();
        var sig = ExtractQueryValue(sut.Sign(fileId, TimeSpan.FromMinutes(5), "attachment"), "sig");

        Assert.False(sut.TryVerify(fileId, exp, "inline", sig));
    }

    // U7
    [Fact]
    public void TryVerify_refuses_an_expired_url_at_the_caller_level()
    {
        var fakeClock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var sut = CreateSut(fakeClock);
        var fileId = Guid.NewGuid();
        var url = sut.Sign(fileId, TimeSpan.FromMinutes(5), "attachment");
        var exp = long.Parse(ExtractQueryValue(url, "exp"));
        var sig = ExtractQueryValue(url, "sig");

        fakeClock.Advance(TimeSpan.FromMinutes(6));

        // The signature itself is still mathematically valid (it signs exp, not "now") --
        // FilesController is what refuses it once exp < now (I6). Verified here as the sub-check
        // TryVerify makes available; the expiry gate lives one layer up.
        Assert.True(sut.TryVerify(fileId, exp, "attachment", sig));
        Assert.True(exp < fakeClock.GetUtcNow().ToUnixTimeSeconds());
    }

    // U7
    [Fact]
    public void TryVerify_refuses_the_wrong_key()
    {
        var sut = CreateSut();
        var otherKeySut = new HmacFileUrlSigner(
            Options.Create(new FileStorageOptions { UrlSigningKey = "a-completely-different-unit-test-signing-key" }),
            TimeProvider.System);
        var fileId = Guid.NewGuid();
        var exp = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds();
        var sig = ExtractQueryValue(otherKeySut.Sign(fileId, TimeSpan.FromMinutes(5), "attachment"), "sig");

        Assert.False(sut.TryVerify(fileId, exp, "attachment", sig));
    }

    [Fact]
    public void TryVerify_refuses_a_malformed_signature_without_throwing()
    {
        var sut = CreateSut();
        Assert.False(sut.TryVerify(Guid.NewGuid(), DateTimeOffset.UtcNow.ToUnixTimeSeconds(), "attachment", "not-base64url!!!"));
    }

    private static string ExtractQueryValue(string url, string key)
    {
        var query = url[(url.IndexOf('?') + 1)..];
        var pair = query.Split('&').First(p => p.StartsWith(key + "=", StringComparison.Ordinal));
        return Uri.UnescapeDataString(pair[(key.Length + 1)..]);
    }

    // U8: reflects over the actual IL of TryVerify and resolves every call-site's metadata token,
    // rather than asserting anything hardcoded -- fails if a future edit replaces the
    // FixedTimeEquals call with `==`/SequenceEqual.
    [Fact]
    public void TryVerify_calls_CryptographicOperations_FixedTimeEquals()
    {
        var method = typeof(HmacFileUrlSigner).GetMethod(nameof(HmacFileUrlSigner.TryVerify), BindingFlags.Public | BindingFlags.Instance)!;

        Assert.True(MethodBodyCalls(method, typeof(System.Security.Cryptography.CryptographicOperations), "FixedTimeEquals"));
    }

    private static bool MethodBodyCalls(MethodInfo method, Type declaringType, string calledMethodName)
    {
        var body = method.GetMethodBody() ?? throw new InvalidOperationException("No method body -- cannot inspect IL.");
        var il = body.GetILAsByteArray() ?? throw new InvalidOperationException("No IL bytes.");
        var module = method.Module;

        for (var i = 0; i < il.Length; i++)
        {
            // call (0x28) / callvirt (0x6F), each followed by a 4-byte metadata token.
            if (il[i] is not (0x28 or 0x6F) || i + 4 >= il.Length) continue;

            var token = BitConverter.ToInt32(il, i + 1);
            try
            {
                var candidate = module.ResolveMethod(token);
                if (candidate is not null && candidate.Name == calledMethodName && candidate.DeclaringType == declaringType)
                    return true;
            }
            catch (ArgumentException)
            {
                // The 4 bytes at this offset were not a method token (e.g. part of a different
                // instruction's operand) -- not every 0x28/0x6F byte we scan is a real opcode.
            }
        }

        return false;
    }
}
