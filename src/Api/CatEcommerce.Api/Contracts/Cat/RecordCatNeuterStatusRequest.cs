namespace CatEcommerce.Api.Contracts.Cat;

public sealed class RecordCatNeuterStatusRequest
{
    public required string NeuterStatus { get; init; }
    public DateOnly? NeuteredOn { get; init; }
}
