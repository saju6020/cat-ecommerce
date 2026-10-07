using System.Reflection;
using System.Text.RegularExpressions;
using CatEcommerce.Domain.Aggregates.StoredFile;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;

namespace CatEcommerce.Domain.Tests.Common;

/// <summary>U12: "Reflect over IFileStore, IMalwareScanner and every DTO: no member named
/// /force|bypass|skipScan|allowAny/i".</summary>
public sealed partial class FilePortSurfaceReflectionTests
{
    public static IEnumerable<object[]> FilePortAndDtoTypes()
    {
        yield return [typeof(IFileStore)];
        yield return [typeof(IMalwareScanner)];
        yield return [typeof(IFileUrlSigner)];
        yield return [typeof(FilePutRequest)];
        yield return [typeof(FileStoreResult<>)];
        yield return [typeof(ScanResult)];
        yield return [typeof(StoredFileRegistrationDto)];
        yield return [typeof(StoredFileAggregate)];
    }

    [Theory]
    [MemberData(nameof(FilePortAndDtoTypes))]
    public void No_member_offers_a_way_to_force_bypass_or_skip_the_scan(Type type)
    {
        var members = type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);

        var offending = members.Where(m => ForbiddenNamePattern().IsMatch(m.Name)).ToList();

        Assert.True(offending.Count == 0,
            $"{type.Name} has a member matching the forbidden force/bypass/skipScan/allowAny pattern: " +
            string.Join(", ", offending.Select(m => m.Name)));
    }

    [GeneratedRegex("force|bypass|skipScan|allowAny", RegexOptions.IgnoreCase)]
    private static partial Regex ForbiddenNamePattern();
}
