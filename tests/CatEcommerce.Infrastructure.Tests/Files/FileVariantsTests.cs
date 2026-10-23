using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Infrastructure.Tests.Files;

/// <summary>ADR-370: which files have resized copies, and where the copies are stored.</summary>
public sealed class FileVariantsTests
{
    [Fact]
    public void Only_product_photos_have_copies()
    {
        Assert.Equal([FileVariants.Card, FileVariants.Detail], FileVariants.For(FilePurpose.ProductImage));
        Assert.Empty(FileVariants.For(FilePurpose.ClinicalPhoto));
        Assert.Empty(FileVariants.For(FilePurpose.CatProfilePhoto));
    }

    [Fact]
    public void A_copy_sits_beside_its_original_with_its_own_suffix()
    {
        Assert.Equal("ProductImage/2026/10/abc.w480.webp", FileVariants.Card.KeyFor("ProductImage/2026/10/abc"));
        Assert.Equal("ProductImage/2026/10/abc.w1200.webp", FileVariants.Detail.KeyFor("ProductImage/2026/10/abc"));
    }

    [Theory]
    [InlineData("ProductImage/2026/10/abc", 2)]
    [InlineData("ClinicalPhoto/2026/10/abc", 0)]
    [InlineData("LabResult/2026/10/ProductImage", 0)]
    public void A_store_finds_the_copies_to_delete_from_the_key_alone(string storageKey, int copies)
    {
        Assert.Equal(copies, FileVariants.ForStorageKey(storageKey).Count);
    }
}
