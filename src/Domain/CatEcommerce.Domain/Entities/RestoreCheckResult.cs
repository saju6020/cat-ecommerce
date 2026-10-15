namespace CatEcommerce.Domain.Entities;

/// <summary>HO-XCT-001 section 8.5. One named check of a restore drill (for example <c>migrations-current</c>). Owned by <c>RestoreTestRecordAggregate</c>.</summary>
public sealed class RestoreCheckResult
{
    public const int MaxNameLength = 100;
    public const int MaxDetailLength = 500;

    public Guid Id { get; private set; }
    public string Name { get; private set; } = default!;
    public bool Passed { get; private set; }
    public string? Detail { get; private set; }

    private RestoreCheckResult() { }

    public RestoreCheckResult(Guid id, string name, bool passed, string? detail)
    {
        Id = id;
        Name = name;
        Passed = passed;
        Detail = detail;
    }
}
