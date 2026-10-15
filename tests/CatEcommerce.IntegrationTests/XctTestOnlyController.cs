using CatEcommerce.Api.Authorization;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.IntegrationTests;

/// <summary>
/// HO-XCT-001 I-A5. Exists only in the test assembly, and only <see cref="XctApiFactory"/> adds it as an application part, so it
/// can never be reachable from the shipped host. It provokes a refused hard delete through real HTTP: a code path that calls
/// <c>DbContext.Remove</c> on a retained record must answer 409 RecordIsRetained and leave the row in place.
/// </summary>
[ApiController]
[Route("api/test-only/xct")]
public sealed class XctTestOnlyController(CatEcommerceDbContext db) : ControllerBase
{
    [HttpPost("remove-cat/{catId:guid}")]
    [PublicEndpoint]
    [CsrfExempt]
    public async Task<IActionResult> RemoveCat(Guid catId)
    {
        var cat = await db.Set<CatAggregate>().FirstAsync(c => c.Id == catId);
        db.Remove(cat);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
