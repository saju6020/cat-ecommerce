using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.ValueObjects;

namespace CatEcommerce.Domain.Tests.ValueObjects;

/// <summary>HO-ACC-001 §15, U-A3.</summary>
public sealed class EmailAddressTests
{
    [Fact]
    public void Create_TrimsAndLowerCases()
    {
        var email = EmailAddress.Create("  Foo@Example.COM ");
        Assert.Equal("foo@example.com", email.Value);
    }

    [Theory]
    [InlineData("a@b")]
    [InlineData("@x.com")]
    [InlineData("")]
    [InlineData("noatsign.com")]
    public void Create_WithInvalidInput_Throws(string raw)
    {
        var ex = Assert.Throws<DomainRuleViolationException>(() => EmailAddress.Create(raw));
        Assert.Equal("InvalidEmailAddress", ex.ErrorCode);
    }

    [Fact]
    public void Create_DifferentCase_AreEqual()
    {
        var a = EmailAddress.Create("Foo@X.com");
        var b = EmailAddress.Create("foo@x.com");
        Assert.Equal(a, b);
    }
}
