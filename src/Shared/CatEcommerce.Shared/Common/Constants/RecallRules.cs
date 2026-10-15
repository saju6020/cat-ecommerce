namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-IMP-001 §8.12 / §10.3.</summary>
public static class RecallRules
{
    public const int MaxRecipientsPerRun = 5000;
    public const int ReasonMaxLength = 1000;
    public const int AdviceMaxLength = 500;

    public static string DeduplicationKey(Guid batchId, Guid customerId) => $"recall:{batchId}:{customerId}";
}
