using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Infrastructure.Files;

/// <summary>HO-FIL-001 §8.1: <c>StorageKey</c> is server-generated,
/// <c>{purpose}/{yyyy}/{MM}/{id:N}</c> -- never derived from the uploaded name. Shared by every
/// <c>IFileStore</c> adapter so the format is identical regardless of which one is active.</summary>
public static class StorageKeyFactory
{
    public static string Build(FilePurpose purpose, Guid fileId, DateTimeOffset uploadedOn) =>
        $"{purpose.DisplayName}/{uploadedOn.UtcDateTime:yyyy}/{uploadedOn.UtcDateTime:MM}/{fileId:N}";
}
