using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Domain.Aggregates.Notification;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Product;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-SHP-001 / HO-SHP-002 open backend backlog rows (BUGS-SHP-BE), end to end. Every test names the row it
/// covers and fails without that row's fix. <c>[Collection("Notifications")]</c>: rows 33/34 run the shop
/// worker pass, which scans the shared, never-truncated alert table (see ShopWishlistAndStockAlertEndpointTests).
/// </summary>
[Collection("Notifications")]
public sealed partial class ShopBacklogFixesEndpointTests(ShopApiFactory factory) : ShopIntegrationTestBase(factory)
{
    private static string NewBrand(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..20];

    private async Task ExecuteSqlAsync(FormattableString sql)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync(sql);
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────

    private async Task UnlistAsync(HttpClient staff, Guid productId)
    {
        var detail = await staff.GetFromJsonAsync<JsonElement>($"/api/staff/shop/products/{productId}", Json);
        var update = await staff.PutAsJsonAsync($"/api/staff/shop/products/{productId}", new
        {
            expectedVersion = detail.GetProperty("version").GetInt32(), name = detail.GetProperty("name").GetString(),
            brand = detail.GetProperty("brand").GetString(), category = detail.GetProperty("category").GetString(),
            taxClassId = detail.GetProperty("taxClassId").GetGuid(), listedOnStorefront = false
        }, Json);
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);
    }
}
