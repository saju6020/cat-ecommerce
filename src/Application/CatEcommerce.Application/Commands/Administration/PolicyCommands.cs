using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Administration;

public sealed class CreatePolicyDocumentCommand : Command
{
    public required Guid PolicyDocumentId { get; init; }
    public required string Key { get; init; }
    public required string Title { get; init; }
    public required bool RequiresAcceptance { get; init; }
}

public sealed class PublishPolicyVersionCommand : Command
{
    public required Guid PolicyVersionId { get; init; }
    public required string PolicyKey { get; init; }
    public required string Body { get; init; }
    public required string ChangeSummary { get; init; }
}

public sealed class AcceptPolicyVersionCommand : Command
{
    public required string PolicyKey { get; init; }
    public required int VersionNumber { get; init; }
    public string? IpAddress { get; init; }
}
