namespace CatEcommerce.Domain.Models;

/// <summary>HO-NTF-001 §8.11/§12.4 -- the result of C9 (<c>UnsubscribeFromMarketingCommand</c>).</summary>
public sealed record UnsubscribeOutcome(string Channel, bool WasSubscribed);
