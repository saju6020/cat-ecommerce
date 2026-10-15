namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-ORD-001 §8.3 / V1 -- the next order number, <c>ORD-{yyyy}-{n:D6}</c>, from <c>ordering.order_number_seq</c>. The sequence never rolls back, so an
/// order whose placement fails leaves a gap: an order number is not an invoice number, and no caller may assume contiguity. Implemented in Infrastructure.
/// </summary>
public interface IOrderNumberGenerator
{
    Task<string> NextAsync(DateTimeOffset now, CancellationToken ct = default);
}
