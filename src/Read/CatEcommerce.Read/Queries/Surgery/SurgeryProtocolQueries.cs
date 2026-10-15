using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Surgery;

/// <summary>HO-SRG-001 §12.6 (US-SRG-07) -- every theatre service with its protocol, or the shipped defaults marked <c>isDefault</c>.</summary>
public sealed class GetSurgeryProtocolsQuery : Query<IReadOnlyList<SurgeryProtocolViewModel>>
{
}
