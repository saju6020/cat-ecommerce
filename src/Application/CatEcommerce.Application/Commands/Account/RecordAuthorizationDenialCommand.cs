using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Account;

public sealed class RecordAuthorizationDenialCommand : Command
{
    public required string Action { get; init; }
    public required string Method { get; init; }
    public required string Path { get; init; }
    public string? IpAddress { get; init; }
}
