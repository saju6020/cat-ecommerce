using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CatEcommerce.Shared.Common.Constants;

namespace CatEcommerce.Shared.Common.Helpers;

/// <summary>
/// Every persisted field of a controlled-drug register entry, as primitives, in the order the canonical form writes them (HO-PHM-001 §8.9). The
/// property names are the aggregate's persisted property names exactly -- <c>U-D6</c> compares the two lists, so adding a field to the entry without
/// adding it here fails the build.
/// </summary>
public sealed record ControlledDrugCanonicalFields(
    Guid Id,
    long EntryNumber,
    Guid BranchId,
    Guid MedicineId,
    string MedicineName,
    string MedicineStrengthText,
    string MedicineForm,
    string? MedicineControlledSchedule,
    string MedicineDispensingUnit,
    Guid? SkuId,
    Guid? BatchId,
    string? LotNumber,
    DateOnly? BatchExpiresOn,
    string Kind,
    decimal QuantityDelta,
    string? DoseText,
    decimal? WastageQuantity,
    Guid? CatId,
    Guid? CustomerId,
    Guid ActorId,
    Guid? WitnessId,
    string SourceType,
    Guid SourceId,
    Guid? CorrectsEntryId,
    string? CorrectionReason,
    Guid? ReturnedEntryId,
    DateTimeOffset OccurredOn,
    DateTimeOffset RecordedOn,
    string? Notes);

/// <summary>
/// HO-PHM-001 §8.9 (ADR-145) -- the canonical text of an entry that the seal's hash is taken over: every field in a fixed order, invariant culture,
/// separated by U+001F, instants in UTC round-trip form, decimals with two decimal places, absent values as the empty string. Pure and deterministic:
/// the worker seals with it and the verification query recomputes with it.
/// </summary>
public static class ControlledDrugEntryCanonicalForm
{
    public const char Separator = '\u001F';

    /// <summary>The field names, in canonical order.</summary>
    public static IReadOnlyList<string> FieldNames { get; } =
        typeof(ControlledDrugCanonicalFields).GetProperties().Where(p => p.DeclaringType == typeof(ControlledDrugCanonicalFields) && p.Name != "EqualityContract")
            .Select(p => p.Name).ToList();

    public static string Build(ControlledDrugCanonicalFields e)
    {
        var parts = new[]
        {
            Guid(e.Id), Number(e.EntryNumber), Guid(e.BranchId), Guid(e.MedicineId), e.MedicineName, e.MedicineStrengthText, e.MedicineForm,
            e.MedicineControlledSchedule ?? "", e.MedicineDispensingUnit, Guid(e.SkuId), Guid(e.BatchId), e.LotNumber ?? "",
            e.BatchExpiresOn?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "", e.Kind, Decimal(e.QuantityDelta), e.DoseText ?? "",
            e.WastageQuantity is { } w ? Decimal(w) : "", Guid(e.CatId), Guid(e.CustomerId), Guid(e.ActorId), Guid(e.WitnessId), e.SourceType, Guid(e.SourceId),
            Guid(e.CorrectsEntryId), e.CorrectionReason ?? "", Guid(e.ReturnedEntryId), Instant(e.OccurredOn), Instant(e.RecordedOn), e.Notes ?? ""
        };

        return string.Join(Separator, parts);
    }

    /// <summary><c>SHA-256(previousHash || canonical form)</c>, lower-case hex (64 characters).</summary>
    public static string Hash(string previousHash, ControlledDrugCanonicalFields entry)
    {
        var bytes = Encoding.UTF8.GetBytes(previousHash + Build(entry));
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    public static string GenesisHash => ControlledDrugRules.ZeroHash;

    private static string Guid(Guid? value) => value?.ToString("N") ?? "";
    private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);
    private static string Decimal(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    // Postgres timestamptz keeps microseconds; truncate to that so a value read back hashes the same as the value that was sealed.
    private static string Instant(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        var micro = new DateTimeOffset(utc.Ticks - utc.Ticks % 10, TimeSpan.Zero);
        return micro.ToString("yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'", CultureInfo.InvariantCulture);
    }
}
