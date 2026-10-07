namespace CatEcommerce.Shared.Common.Constants;

public static class CatPhotoRules
{
    public const long MaxSizeInBytes = 5 * 1024 * 1024;
    public const int MaxPhotosPerCat = 10;

    public static readonly string[] AllowedContentTypes =
        ["image/jpeg", "image/png", "image/webp"];
}
