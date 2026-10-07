using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Account;

/// <summary>Q-J1 -- POST /api/customers/me/personal-data-export.</summary>
public sealed class GetPersonalDataExportQuery : Query<PersonalDataExportViewModel?>
{
    public required Guid CustomerId { get; init; }
}
