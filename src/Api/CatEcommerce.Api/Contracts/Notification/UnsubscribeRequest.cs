namespace CatEcommerce.Api.Contracts.Notification;

public sealed class UnsubscribeRequest
{
    public required string Token { get; init; }
}
