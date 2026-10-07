using CatEcommerce.Infrastructure.Files;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Infrastructure.Tests.Files;

/// <summary>U6.</summary>
public sealed class StorageKeyFactoryTests
{
    [Fact]
    public void Build_returns_distinct_keys_for_two_files_uploaded_in_the_same_minute()
    {
        var now = DateTimeOffset.UtcNow;
        var keyA = StorageKeyFactory.Build(FilePurpose.ProductImage, Guid.NewGuid(), now);
        var keyB = StorageKeyFactory.Build(FilePurpose.ProductImage, Guid.NewGuid(), now);

        Assert.NotEqual(keyA, keyB);
    }

    [Fact]
    public void Build_never_contains_a_file_name()
    {
        var key = StorageKeyFactory.Build(FilePurpose.ProductImage, Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.DoesNotContain("passwd", key, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain('.', key);
    }

    [Fact]
    public void Build_has_the_documented_shape()
    {
        var fileId = Guid.NewGuid();
        var uploadedOn = new DateTimeOffset(2026, 3, 7, 12, 0, 0, TimeSpan.Zero);

        var key = StorageKeyFactory.Build(FilePurpose.ClinicalPhoto, fileId, uploadedOn);

        Assert.Equal($"ClinicalPhoto/2026/03/{fileId:N}", key);
    }
}
