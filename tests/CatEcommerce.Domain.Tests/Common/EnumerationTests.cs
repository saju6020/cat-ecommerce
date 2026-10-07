using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Tests.Common;

/// <summary>U27 (HO-CAT-001 section 15).</summary>
public sealed class EnumerationTests
{
    [Fact]
    public void FromValue_ReturnsMatchingMember()
    {
        Assert.Same(CatSex.Female, Enumeration.FromValue<CatSex>(2));
    }

    [Fact]
    public void FromDisplayName_ReturnsMatchingMember()
    {
        Assert.Same(VaccinationStatus.Overdue, Enumeration.FromDisplayName<VaccinationStatus>("Overdue"));
    }

    [Fact]
    public void GetAll_ReturnsEveryMember()
    {
        var all = Enumeration.GetAll<CatSex>().ToList();

        Assert.Equal(3, all.Count);
        Assert.Contains(CatSex.Male, all);
        Assert.Contains(CatSex.Female, all);
        Assert.Contains(CatSex.Unknown, all);
    }

    [Fact]
    public void Equality_SameValueSameType_AreEqual()
    {
        Assert.Equal(CatSex.Male, Enumeration.FromValue<CatSex>(1));
        Assert.True(CatSex.Male == Enumeration.FromValue<CatSex>(1));
    }

    [Fact]
    public void FromValue_UnknownValue_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Enumeration.FromValue<CatSex>(99));
    }
}
