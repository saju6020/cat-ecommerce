namespace CatEcommerce.Read.ViewModels;

/// <summary>HO-ACC-001 §12.3 (Q-F2, Q-I1).</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int PageNumber, int PageSize, int TotalCount);
