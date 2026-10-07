namespace CatEcommerce.Api.Contracts.Product;

public sealed class SubmitProductReviewRequest
{
    public required Guid ProductId { get; init; }
    public required Guid SkuId { get; init; }
    public required int Rating { get; init; }
    public string? Title { get; init; }
    public required string Body { get; init; }
}

public sealed class EditProductReviewRequest
{
    public required int Rating { get; init; }
    public string? Title { get; init; }
    public required string Body { get; init; }
}

public sealed class RejectProductReviewRequest
{
    public required string Reason { get; init; }
    public string? Note { get; init; }
}

public sealed class RequestStockAlertRequest
{
    public required Guid SkuId { get; init; }
}
