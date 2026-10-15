namespace CatEcommerce.Domain.Entities;

/// <summary>HO-BIL-001 §8.1/§11.4 C22 -- how much of one courier-collected COD payment a remittance
/// covers. Shortfall of a collection = collected − Σ remitted across all remittances (ADR-189).</summary>
public sealed class RemittanceLine
{
    public Guid Id { get; private set; }
    public Guid PaymentId { get; private set; }
    public decimal AmountRemitted { get; private set; }

    private RemittanceLine() { }

    public RemittanceLine(Guid id, Guid paymentId, decimal amountRemitted)
    {
        Id = id;
        PaymentId = paymentId;
        AmountRemitted = amountRemitted;
    }
}
