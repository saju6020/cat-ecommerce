using CatEcommerce.Shared.Common.Constants;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// ADR-370: makes the resized WebP copies of an image (<see cref="FileVariants"/>). The camera's rotation is applied and
/// no metadata (EXIF, GPS) is carried into a copy. Returns null when the bytes cannot be decoded as an image, so the
/// upload can be refused before anything is stored.
/// </summary>
public interface IImageVariantGenerator
{
    IReadOnlyList<GeneratedVariant>? Generate(Stream image, IReadOnlyList<FileVariant> variants);
}

public sealed record GeneratedVariant(FileVariant Variant, byte[] Content);
