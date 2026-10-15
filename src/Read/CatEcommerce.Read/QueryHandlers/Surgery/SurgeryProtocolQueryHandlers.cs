using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Surgery;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Surgery;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Surgery;

/// <summary>Two reads, no per-row query: every active theatre service, then every protocol (ADR-167). A service without a protocol shows the defaults.</summary>
public sealed class GetSurgeryProtocolsQueryHandler(CatEcommerceDbContext db)
    : IQueryHandlerAsync<GetSurgeryProtocolsQuery, IReadOnlyList<SurgeryProtocolViewModel>>
{
    public async Task<QueryResponse<IReadOnlyList<SurgeryProtocolViewModel>>> HandleAsync(GetSurgeryProtocolsQuery query)
    {
        var services = await db.Set<ClinicServiceAggregate>().AsNoTracking()
            .Where(s => !s.IsMarkedToDelete && s.RequiredRoomKind == RoomKind.Theatre)
            .OrderBy(s => s.Name)
            .Select(s => new { s.Id, s.Name, s.Category })
            .ToListAsync();

        var protocols = await db.Set<SurgeryProtocolAggregate>().AsNoTracking()
            .Where(p => !p.IsMarkedToDelete)
            .Select(p => new
            {
                p.ProtocolServiceId, p.FastingInstructions, p.FastingHours, p.PostOpInstructions, p.UpdatedOn, p.Version,
                FollowUps = p.FollowUps.OrderBy(f => f.DayOffset).Select(f => new { Kind = f.Kind, f.DayOffset, f.WindowFromDay, f.WindowToDay }).ToList()
            })
            .ToListAsync();
        var byService = protocols.ToDictionary(p => p.ProtocolServiceId);

        var result = services.Select(s =>
        {
            if (byService.TryGetValue(s.Id, out var p))
                return new SurgeryProtocolViewModel(
                    s.Id, s.Name, s.Category.DisplayName, false, p.FastingInstructions, p.FastingHours, p.PostOpInstructions,
                    p.FollowUps.Select(f => new SurgeryProtocolFollowUpViewModel(f.Kind.DisplayName, f.DayOffset, f.WindowFromDay, f.WindowToDay)).ToList(), p.UpdatedOn, p.Version);

            return new SurgeryProtocolViewModel(
                s.Id, s.Name, s.Category.DisplayName, true, SurgeryProtocolDefaults.FastingInstructions, SurgeryProtocolDefaults.FastingHours,
                SurgeryProtocolDefaults.PostOpInstructions,
                SurgeryProtocolDefaults.FollowUps.Select(f => new SurgeryProtocolFollowUpViewModel(f.Kind, f.DayOffset, f.WindowFromDay, f.WindowToDay)).ToList(), null, 0);
        }).ToList();

        return new QueryResponse<IReadOnlyList<SurgeryProtocolViewModel>> { Result = result };
    }
}
