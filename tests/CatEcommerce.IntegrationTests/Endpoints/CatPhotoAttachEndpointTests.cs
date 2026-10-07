using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-FIL-001 §1 Amended (10) / the carried HO-CAT-002-SECURITY item. Covers the surface the
/// ARCHITECTURE and SECURITY second-pass gates asked to see proven end to end:
/// - a customer uploading and attaching their own cat's photo, and being refused (404, by
///   OwnerScope) for a cat they do not own;
/// - a concurrent attach of one file to two different cats yielding exactly one winner, via
///   <c>clinic.cat_photo</c>'s own unique index on <c>FileId</c> -- <c>StoredFileAggregate</c> no
///   longer has any state of its own to race on (gate-fix 2026-09-27, ARCHITECTURE HIGH second
///   pass: <c>IsEligibleForAttach</c> is side-effect-free).
/// </summary>
public sealed class CatPhotoAttachEndpointTests(FilesApiFactory factory) : FilesIntegrationTestBase(factory)
{
    // §1 Amended (10): the customer route accepts CatProfilePhoto, and the owning customer can
    // attach it to their own cat.
    [Fact]
    public async Task Customer_uploads_and_attaches_a_photo_to_their_own_cat_and_it_is_served_back()
    {
        var (client, customerId, _) = await RegisterAndSignInCustomerAsync();
        var catId = await SeedCatAsync(customerId);

        using var form = BuildUpload(MinimalPng(), "cat.png", "image/png");
        var upload = await client.PostAsync("/api/customers/me/files?purpose=CatProfilePhoto", form);
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        var fileId = (await upload.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("fileId").GetGuid();

        var attach = await client.PostAsJsonAsync($"/api/cats/{catId}/photos", new { fileId, isPrimary = true }, Json);
        Assert.Equal(HttpStatusCode.Created, attach.StatusCode);

        var profile = await client.GetAsync($"/api/cats/{catId}");
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
        var body = await profile.Content.ReadFromJsonAsync<JsonElement>(Json);
        var photo = Assert.Single(body.GetProperty("photos").EnumerateArray());
        Assert.Equal(fileId, photo.GetProperty("fileId").GetGuid());
        Assert.False(string.IsNullOrEmpty(photo.GetProperty("url").GetString()));

        var photoFetch = await client.GetAsync(photo.GetProperty("url").GetString());
        Assert.Equal(HttpStatusCode.OK, photoFetch.StatusCode);
        Assert.Equal("image/png", photoFetch.Content.Headers.ContentType?.MediaType);
    }

    // §1 Amended (10): a customer may upload their own CatProfilePhoto file, but attaching it to a
    // cat they do not own is refused by OwnerScope before the handler ever runs.
    [Fact]
    public async Task Customer_cannot_attach_their_own_file_to_another_customers_cat()
    {
        var (ownerClient, ownerId, _) = await RegisterAndSignInCustomerAsync();
        var ownerCatId = await SeedCatAsync(ownerId);

        var (otherClient, _, _) = await RegisterAndSignInCustomerAsync();
        using var form = BuildUpload(MinimalPng(), "cat.png", "image/png");
        var upload = await otherClient.PostAsync("/api/customers/me/files?purpose=CatProfilePhoto", form);
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        var fileId = (await upload.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("fileId").GetGuid();

        var attach = await otherClient.PostAsJsonAsync($"/api/cats/{ownerCatId}/photos", new { fileId, isPrimary = true }, Json);

        Assert.Equal(HttpStatusCode.NotFound, attach.StatusCode);

        // The file is untouched by the refused attempt -- still eligible for the actual owner to
        // use, since nothing about this cross-owner refusal ever ran the eligibility check at all.
        var ownerAttach = await ownerClient.PostAsJsonAsync($"/api/cats/{ownerCatId}/photos", new { fileId, isPrimary = true }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, ownerAttach.StatusCode); // wrong uploader -- PhotoFileNotEligible, not a crash
    }

    // Gate-fix 2026-09-27 (ARCHITECTURE HIGH, second pass): "one file, one cat" is now enforced only
    // by clinic.cat_photo's unique index on FileId, not by state on StoredFileAggregate. A dozen
    // concurrent attach attempts of the SAME file, split across two different cats, must still
    // leave exactly one cat_photo row referencing it.
    [Fact]
    public async Task Concurrent_attach_of_one_file_to_two_cats_yields_exactly_one_row()
    {
        using var client = await SignedInStaffClientAsync(StaffRoles.Administrator);
        var catAId = await SeedCatAsync(Guid.NewGuid());
        var catBId = await SeedCatAsync(Guid.NewGuid());

        using var form = BuildUpload(MinimalPng(), "cat.png", "image/png");
        var upload = await client.PostAsync("/api/staff/files?purpose=CatProfilePhoto", form);
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        var fileId = (await upload.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("fileId").GetGuid();

        const int attemptsPerCat = 6;
        var attempts = Enumerable.Range(0, attemptsPerCat * 2)
            .Select(i => client.PostAsJsonAsync($"/api/cats/{(i % 2 == 0 ? catAId : catBId)}/photos", new { fileId, isPrimary = true }, Json));

        var responses = await Task.WhenAll(attempts);

        var successCount = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        var refusedCount = responses.Count(r => r.StatusCode == HttpStatusCode.BadRequest);
        Assert.Equal(1, successCount);
        Assert.Equal(responses.Length - 1, refusedCount);

        foreach (var refused in responses.Where(r => r.StatusCode == HttpStatusCode.BadRequest))
        {
            var body = await refused.Content.ReadFromJsonAsync<JsonElement>(Json);
            Assert.Equal(BusinessViolationCodes.PhotoFileNotEligible, body.GetProperty("errors")[0].GetProperty("errorCode").GetString());
        }

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
        var referencingRows = await db.Cats.AsNoTracking()
            .SelectMany(c => c.Photos)
            .CountAsync(p => p.FileId == fileId);
        Assert.Equal(1, referencingRows);
    }
}
