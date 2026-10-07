using CatEcommerce.Infrastructure.Configuration;
using CatEcommerce.Infrastructure.Security.Captcha;
using Microsoft.Extensions.Options;

namespace CatEcommerce.IntegrationTests.Unit;

/// <summary>HO-ACC-001 §15, U-B9/U-B10/U-B11.</summary>
public sealed class MemoryCacheCaptchaServiceTests
{
    private static MemoryCacheCaptchaService CreateService(CaptchaOptions? options = null) =>
        new(Options.Create(options ?? new CaptchaOptions()), new SkiaCaptchaImageRenderer());

    // U-B9
    [Fact]
    public void Issue_OneThousandTimes_ProducesUniqueIds()
    {
        var service = CreateService(new CaptchaOptions { MaxOutstandingChallenges = 2000 });
        var ids = Enumerable.Range(0, 1000).Select(_ => service.Issue().CaptchaId).ToHashSet();
        Assert.Equal(1000, ids.Count);
    }

    // U-B10
    [Fact]
    public void Verify_CorrectAnswerDifferentCaseAndSpaces_ReturnsTrueOnce()
    {
        var service = CreateService(new CaptchaOptions { TestAnswer = "AB12" });
        var challenge = service.Issue();

        Assert.True(service.Verify(challenge.CaptchaId, " ab12 "));
    }

    [Fact]
    public void Verify_ReplayAfterCorrect_ReturnsFalse()
    {
        var service = CreateService(new CaptchaOptions { TestAnswer = "AB12" });
        var challenge = service.Issue();

        Assert.True(service.Verify(challenge.CaptchaId, "AB12"));
        Assert.False(service.Verify(challenge.CaptchaId, "AB12"));
    }

    [Fact]
    public void Verify_ReplayAfterWrong_ReturnsFalse()
    {
        var service = CreateService(new CaptchaOptions { TestAnswer = "AB12" });
        var challenge = service.Issue();

        Assert.False(service.Verify(challenge.CaptchaId, "WRONG"));
        Assert.False(service.Verify(challenge.CaptchaId, "AB12"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Verify_NullOrBlank_ReturnsFalseWithoutThrowing(string? answer)
    {
        var service = CreateService();
        var challenge = service.Issue();

        var exception = Record.Exception(() => service.Verify(challenge.CaptchaId, answer));
        Assert.Null(exception);
        Assert.False(service.Verify(challenge.CaptchaId, answer));
    }

    [Fact]
    public void Verify_UnknownId_ReturnsFalse()
    {
        var service = CreateService();
        Assert.False(service.Verify("does-not-exist", "AB12"));
    }

    // SECURITY gate LOW: TryGetValue then Remove as two separate calls let concurrent submits of
    // one solved challenge all read the answer before any of them removed it. Verified by the
    // gate's own probe (200 challenges x 16 parallel verifies each -> 312 successes, 88 challenges
    // accepted more than once). With the atomic get-and-remove, exactly one submit out of many
    // parallel ones for the same challenge may ever succeed.
    [Fact]
    public async Task Verify_ManyParallelSubmitsOfOneChallenge_ExactlyOneSucceeds()
    {
        var service = CreateService(new CaptchaOptions { TestAnswer = "AB12" });

        for (var round = 0; round < 50; round++)
        {
            var challenge = service.Issue();

            var results = await Task.WhenAll(Enumerable.Range(0, 16)
                .Select(_ => Task.Run(() => service.Verify(challenge.CaptchaId, "AB12"))));

            Assert.Equal(1, results.Count(r => r));
        }
    }

    // U-B11
    [Fact]
    public void Issue_BeyondMaxOutstanding_DoesNotThrowAndStaysBounded()
    {
        var service = CreateService(new CaptchaOptions { MaxOutstandingChallenges = 5, TestAnswer = "AB12" });
        var challenges = new List<CaptchaChallenge>();

        var exception = Record.Exception(() =>
        {
            for (var i = 0; i < 50; i++) challenges.Add(service.Issue());
        });

        Assert.Null(exception);

        // Bounded: not every one of the 50 issued challenges can still be outstanding when the
        // cache is limited to 5 -- at least one of the earliest must have been evicted.
        var stillVerifiable = challenges.Count(c => service.Verify(c.CaptchaId, "AB12"));
        Assert.True(stillVerifiable < challenges.Count);
    }
}
