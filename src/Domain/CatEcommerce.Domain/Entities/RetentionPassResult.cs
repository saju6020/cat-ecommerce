using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Entities;

/// <summary>HO-XCT-001 section 8.4. One pass of a retention run: counts and codes, never a row value. Owned by <c>RetentionRunAggregate</c>.</summary>
public sealed class RetentionPassResult
{
    public const int MaxErrorCodes = 10;
    public const int MaxErrorCodeLength = 100;

    public Guid Id { get; private set; }
    public RetentionPassKind Kind { get; private set; } = default!;
    public DateTimeOffset? Cutoff { get; private set; }
    public int Examined { get; private set; }
    public int Affected { get; private set; }
    public int Held { get; private set; }
    public int Failed { get; private set; }
    public string[] ErrorCodes { get; private set; } = [];
    public int DurationMs { get; private set; }

    private RetentionPassResult() { }

    public RetentionPassResult(Guid id, RetentionPassKind kind, DateTimeOffset? cutoff, int examined, int affected, int held, int failed, IEnumerable<string> errorCodes, int durationMs)
    {
        Id = id;
        Kind = kind;
        Cutoff = cutoff;
        Examined = examined;
        Affected = affected;
        Held = held;
        Failed = failed;
        ErrorCodes = errorCodes.Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Length > MaxErrorCodeLength ? c[..MaxErrorCodeLength] : c)
            .Distinct(StringComparer.Ordinal).Take(MaxErrorCodes).ToArray();
        DurationMs = durationMs;
    }

    public bool HadErrors => Failed > 0 || ErrorCodes.Length > 0;
}
