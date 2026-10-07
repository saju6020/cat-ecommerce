using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Account;

/// <summary>No payload -- the customer id is taken from <c>UserContext.UserId</c> (§11.1).</summary>
public sealed class RecordPersonalDataExportCommand : Command;
