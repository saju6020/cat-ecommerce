using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.DependencyInjection;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-ACC-001 §16, Duplicates and merge (I). Regression guard for
/// <c>GetDuplicateCustomerGroupsQueryHandler</c>'s rewrite (ARCHITECTURE gate, MEDIUM -- database
/// candidate-narrowing plus a batched cats query instead of one query per group member) -- proves
/// two customers who share an additional phone number still land in the same duplicate group.
/// </summary>
public sealed class DuplicateCustomerGroupsEndpointTests(AccountsApiFactory factory) : AccountsIntegrationTestBase(factory)
{
    private async Task<HttpClient> AdministratorClientAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<StaffAccountAggregate>>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var mobile = NewMobile();
        var staff = new StaffAccountAggregate(Guid.NewGuid());
        staff.Create(new StaffAccountCreationDto(
            "Merge Admin", MobileNumber.Create(mobile), null, ["Administrator"],
            new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
        await repository.SaveAsync(staff, EventPublishOption.InMemoryPublishOnly);

        const string password = "adminpassword1";
        staff.SetPassword(new PasswordSetDto(hasher.Hash(password), new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
        await repository.UpdateAsync(staff, EventPublishOption.InMemoryPublishOnly);

        var client = CreateClient();
        var signIn = await client.PostAsJsonAsync("/api/staff/auth/sign-in", new { identifier = mobile, password }, Json);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        client.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookieValue(signIn, "pc_csrf"));
        return client;
    }

    private async Task AddSharedAdditionalNumberAsync(string mobile, string password, string sharedNumber)
    {
        using var client = CreateClient();
        var signIn = await client.PostAsJsonAsync("/api/auth/sign-in", new { identifier = mobile, password }, Json);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        client.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookieValue(signIn, "pc_csrf"));

        var update = await client.PutAsJsonAsync("/api/customers/me", new
        {
            fullName = "Integration Test Customer",
            additionalPhoneNumbers = new[] { sharedNumber }
        }, Json);
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);
    }

    [Fact]
    public async Task TwoCustomers_SharingAnAdditionalNumber_AppearInTheSameDuplicateGroup()
    {
        using var registerClient = CreateClient();
        var (customerAId, mobileA, passwordA) = await RegisterCustomerAsync(registerClient);
        var (customerBId, mobileB, passwordB) = await RegisterCustomerAsync(registerClient);

        var sharedNumber = NewMobile();
        await AddSharedAdditionalNumberAsync(mobileA, passwordA, sharedNumber);
        await AddSharedAdditionalNumberAsync(mobileB, passwordB, sharedNumber);

        using var adminClient = await AdministratorClientAsync();
        // §16.1: catecommerce_dev is shared and never cleaned between test runs, so the default
        // page size cannot be relied on to include this test's own group among however many
        // duplicate groups earlier runs (and other tests in this same run) have accumulated. A
        // large explicit page size keeps this test about the grouping logic, not about paging --
        // capped at PagingRules.MaxPageSize (100, integrator fix 2026-09-26), the largest value
        // this endpoint now accepts (this file's own pageSize=1000 predates that cap).
        var response = await adminClient.GetAsync("/api/staff/customers/duplicates?pageSize=100");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var groups = body.GetProperty("items").EnumerateArray().ToList();

        var matchingGroup = groups.FirstOrDefault(g => g.GetProperty("members").EnumerateArray()
            .Any(m => m.GetProperty("customerId").GetGuid() == customerAId));

        Assert.True(matchingGroup.ValueKind == JsonValueKind.Object, "Expected a duplicate group containing customer A.");
        var memberIds = matchingGroup.GetProperty("members").EnumerateArray()
            .Select(m => m.GetProperty("customerId").GetGuid())
            .ToList();

        Assert.Contains(customerAId, memberIds);
        Assert.Contains(customerBId, memberIds);
    }

    // SECURITY gate LOW (second pass, HO-ACC-001 §1 Amended (18)): a pairing based only on an
    // unverified additional number must be labelled "Additional", not treated the same as a
    // genuine shared primary mobile -- staff should confirm identity before merging or attaching
    // cats from this pairing (mirrors Q-H2's own mobileMatchedOn label).
    [Fact]
    public async Task TwoCustomers_SharingAnAdditionalNumber_AreBothLabelledAdditionalNotPrimary()
    {
        using var registerClient = CreateClient();
        var (customerAId, mobileA, passwordA) = await RegisterCustomerAsync(registerClient);
        var (customerBId, mobileB, passwordB) = await RegisterCustomerAsync(registerClient);

        var sharedNumber = NewMobile();
        await AddSharedAdditionalNumberAsync(mobileA, passwordA, sharedNumber);
        await AddSharedAdditionalNumberAsync(mobileB, passwordB, sharedNumber);

        using var adminClient = await AdministratorClientAsync();
        // Capped at PagingRules.MaxPageSize (100, integrator fix 2026-09-26) -- see the comment on
        // the sibling test above.
        var response = await adminClient.GetAsync("/api/staff/customers/duplicates?pageSize=100");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var groups = body.GetProperty("items").EnumerateArray().ToList();

        var matchingGroup = groups.FirstOrDefault(g => g.GetProperty("members").EnumerateArray()
            .Any(m => m.GetProperty("customerId").GetGuid() == customerAId));
        Assert.True(matchingGroup.ValueKind == JsonValueKind.Object, "Expected a duplicate group containing customer A.");

        var members = matchingGroup.GetProperty("members").EnumerateArray().ToList();
        var memberA = members.Single(m => m.GetProperty("customerId").GetGuid() == customerAId);
        var memberB = members.Single(m => m.GetProperty("customerId").GetGuid() == customerBId);

        Assert.Equal("Additional", memberA.GetProperty("mobileMatchedOn").GetString());
        Assert.Equal("Additional", memberB.GetProperty("mobileMatchedOn").GetString());
    }

    // HO-ACC-002 CR MEDIUM 2 (first-pass gate fix round, item 13): §11.12's prose already asked for
    // recordStatus/registrationChannel per duplicate-group member; DuplicateCustomerMemberViewModel
    // didn't carry them until this fix.
    [Fact]
    public async Task TwoSelfRegisteredCustomers_SharingAnAdditionalNumber_ReportRecordStatusAndRegistrationChannelPerMember()
    {
        using var registerClient = CreateClient();
        var (customerAId, mobileA, passwordA) = await RegisterCustomerAsync(registerClient);
        var (customerBId, mobileB, passwordB) = await RegisterCustomerAsync(registerClient);

        var sharedNumber = NewMobile();
        await AddSharedAdditionalNumberAsync(mobileA, passwordA, sharedNumber);
        await AddSharedAdditionalNumberAsync(mobileB, passwordB, sharedNumber);

        using var adminClient = await AdministratorClientAsync();
        var response = await adminClient.GetAsync("/api/staff/customers/duplicates?pageSize=100");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var groups = body.GetProperty("items").EnumerateArray().ToList();
        var matchingGroup = groups.FirstOrDefault(g => g.GetProperty("members").EnumerateArray()
            .Any(m => m.GetProperty("customerId").GetGuid() == customerAId));
        Assert.True(matchingGroup.ValueKind == JsonValueKind.Object, "Expected a duplicate group containing customer A.");

        var members = matchingGroup.GetProperty("members").EnumerateArray().ToList();
        var memberA = members.Single(m => m.GetProperty("customerId").GetGuid() == customerAId);
        var memberB = members.Single(m => m.GetProperty("customerId").GetGuid() == customerBId);

        // RegisterCustomerAsync always sets a password, so a self-registered customer is
        // "Complete"/"SelfService" -- the same derivation GetStaffCustomerQueryHandler already uses.
        Assert.Equal("SelfService", memberA.GetProperty("registrationChannel").GetString());
        Assert.Equal("Complete", memberA.GetProperty("recordStatus").GetString());
        Assert.Equal("SelfService", memberB.GetProperty("registrationChannel").GetString());
        Assert.Equal("Complete", memberB.GetProperty("recordStatus").GetString());
    }

    // HO-ACC-002 CR MEDIUM 2 (first-pass gate fix round, item 13): a walk-in-registered customer
    // (no password set yet) must report "Incomplete"/"Reception", distinguishing it from a
    // self-registered pairing -- proves the fields are actually derived per member, not a constant.
    [Fact]
    public async Task WalkInCustomer_InADuplicateGroup_ReportsIncompleteAndReceptionRegistrationChannel()
    {
        using var registerClient = CreateClient();
        var (selfRegisteredId, mobileA, passwordA) = await RegisterCustomerAsync(registerClient);

        // The walk-in customer has no session of its own (no password yet), so there is no
        // endpoint to give it an additional number -- instead, customer A adds the number FIRST as
        // its own additional number, then a walk-in is registered with that SAME number as its
        // PRIMARY mobile. HO-ACC-001 §1 Amended (16): only a PRIMARY-PRIMARY match refuses walk-in
        // registration -- a primary matching an existing ADDITIONAL number succeeds (`201`) and the
        // two land in the same duplicate group, exactly this scenario.
        var sharedNumber = NewMobile();
        await AddSharedAdditionalNumberAsync(mobileA, passwordA, sharedNumber);

        using var receptionClient = await ReceptionClientAsync();
        var walkIn = await receptionClient.PostAsJsonAsync("/api/staff/customers",
            new { fullName = "Walk-in Duplicate Test", mobileNumber = sharedNumber }, Json);
        Assert.Equal(HttpStatusCode.Created, walkIn.StatusCode);
        var walkInBody = await walkIn.Content.ReadFromJsonAsync<JsonElement>(Json);
        var walkInCustomerId = walkInBody.GetProperty("customerId").GetGuid();

        using var adminClient = await AdministratorClientAsync();
        var response = await adminClient.GetAsync("/api/staff/customers/duplicates?pageSize=100");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var groups = body.GetProperty("items").EnumerateArray().ToList();
        var matchingGroup = groups.FirstOrDefault(g => g.GetProperty("members").EnumerateArray()
            .Any(m => m.GetProperty("customerId").GetGuid() == walkInCustomerId));
        Assert.True(matchingGroup.ValueKind == JsonValueKind.Object, "Expected a duplicate group containing the walk-in customer.");

        var walkInMember = matchingGroup.GetProperty("members").EnumerateArray()
            .Single(m => m.GetProperty("customerId").GetGuid() == walkInCustomerId);

        Assert.Equal("Reception", walkInMember.GetProperty("registrationChannel").GetString());
        Assert.Equal("Incomplete", walkInMember.GetProperty("recordStatus").GetString());
        Assert.NotEqual(selfRegisteredId, walkInCustomerId);
    }

    [Fact]
    public async Task CustomerWithNoSharedIdentifier_NeverAppearsInAnyDuplicateGroup()
    {
        using var registerClient = CreateClient();
        var (soloCustomerId, _, _) = await RegisterCustomerAsync(registerClient);

        using var adminClient = await AdministratorClientAsync();
        var response = await adminClient.GetAsync("/api/staff/customers/duplicates");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var groups = body.GetProperty("items").EnumerateArray().ToList();

        Assert.DoesNotContain(groups, g => g.GetProperty("members").EnumerateArray()
            .Any(m => m.GetProperty("customerId").GetGuid() == soloCustomerId));
    }

    private async Task<HttpClient> ReceptionClientAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<StaffAccountAggregate>>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var mobile = NewMobile();
        var staff = new StaffAccountAggregate(Guid.NewGuid());
        staff.Create(new StaffAccountCreationDto(
            "Merge Test Reception", MobileNumber.Create(mobile), null, ["Reception"],
            new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
        await repository.SaveAsync(staff, EventPublishOption.InMemoryPublishOnly);

        const string password = "receptionpassword1";
        staff.SetPassword(new PasswordSetDto(hasher.Hash(password), new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
        await repository.UpdateAsync(staff, EventPublishOption.InMemoryPublishOnly);

        var client = CreateClient();
        var signIn = await client.PostAsJsonAsync("/api/staff/auth/sign-in", new { identifier = mobile, password }, Json);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        client.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookieValue(signIn, "pc_csrf"));
        return client;
    }

    /// <summary>HO-ADM-001 §12.3 / ADR-043: <c>Cats.Register</c> now requires a signed-in staff or
    /// customer caller -- registers through a fresh Reception session (which honours the explicit
    /// <paramref name="ownerId"/>, unlike a customer caller) rather than the anonymous client this
    /// took before.</summary>
    private async Task<Guid> RegisterCatAsync(HttpClient anyClient, Guid ownerId)
    {
        using var receptionClient = await ReceptionClientAsync();

        var response = await receptionClient.PostAsJsonAsync("/api/cats", new
        {
            ownerId, name = "Merge Test Cat", sex = "Female", dateOfBirthPrecision = "Estimated"
        }, Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        return body.GetProperty("catId").GetGuid();
    }

    /// <summary>
    /// CODE_REVIEW gate, MEDIUM (2026-09-26, third pass): RegisterCatCommandHandler now requires
    /// the owner to be an Active customer, so the HTTP registration endpoint can no longer be used
    /// to put a cat on an already-Merged customer id -- correctly, for a real caller. This test's
    /// own scenario needs exactly that state (a cat whose OwnerId is a customer that has already
    /// been merged, to prove ADR-039's resend-recovery still sweeps it up), so it seeds the cat
    /// directly through the repository instead, the same "SeedCatAsync" pattern
    /// AdministrationIntegrationTestBase uses -- ReassignCatsOnCustomerMergedHandler only ever
    /// queries CatAggregate.OwnerId, never the owning customer's Status, so this is a faithful
    /// stand-in for "a cat the first merge's reassignment loop had not yet reached".
    /// </summary>
    private async Task<Guid> SeedCatOwnedByAsync(Guid ownerId, string name)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<CatAggregate>>();

        var cat = new CatAggregate(Guid.NewGuid());
        cat.Register(new CatRegistrationDto(
            ownerId, name, "Cat", CatSex.Female, null, DateOfBirthPrecision.Estimated, null, null,
            DateOnly.FromDateTime(DateTime.UtcNow),
            new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));

        await repository.SaveAsync(cat, EventPublishOption.InMemoryPublishOnly);
        return cat.Id;
    }

    // I-I2. §4.3: no orders, invoices or balances exist in any context, so this proves the one
    // thing the merge itself is responsible for -- both cats move, the merge is audited via the
    // ordinary change-capture path (ADR-040), and the duplicate's own session stops working
    // (MergeInto rotates SecurityStamp, exactly like SetPassword/Anonymise).
    [Fact]
    public async Task Merge_MovesBothCatsToTheSurvivorAndEndsTheDuplicatesSession()
    {
        using var registerClient = CreateClient();
        var (survivorId, survivorMobile, survivorPassword) = await RegisterCustomerAsync(registerClient);
        var (duplicateId, duplicateMobile, duplicatePassword) = await RegisterCustomerAsync(registerClient);

        var sharedNumber = NewMobile();
        await AddSharedAdditionalNumberAsync(survivorMobile, survivorPassword, sharedNumber);
        await AddSharedAdditionalNumberAsync(duplicateMobile, duplicatePassword, sharedNumber);

        // The duplicate signs in BEFORE the merge -- this is the session I-I2 requires to observe
        // ending as a consequence of the merge itself.
        using var duplicateClient = CreateClient();
        var duplicateSignIn = await duplicateClient.PostAsJsonAsync("/api/auth/sign-in", new { identifier = duplicateMobile, password = duplicatePassword }, Json);
        Assert.Equal(HttpStatusCode.OK, duplicateSignIn.StatusCode);
        duplicateClient.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookieValue(duplicateSignIn, "pc_csrf"));

        var survivorCatId = await RegisterCatAsync(registerClient, survivorId);
        var duplicateCatId = await RegisterCatAsync(registerClient, duplicateId);

        using var adminClient = await AdministratorClientAsync();
        var merge = await adminClient.PostAsJsonAsync($"/api/staff/customers/{duplicateId}/merge", new
        {
            survivingCustomerId = survivorId, reason = "Same person, confirmed by phone"
        }, Json);
        Assert.Equal(HttpStatusCode.OK, merge.StatusCode);
        var mergeBody = await merge.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal(survivorId, mergeBody.GetProperty("survivingCustomerId").GetGuid());
        Assert.False(mergeBody.GetProperty("isReapplication").GetBoolean());

        // HO-ADM-001 §12.3 / ADR-043: Cats.View now requires a signed-in caller -- the already
        // signed-in admin client reads both, since staff bypass the ownership check entirely.
        var survivorCat = await adminClient.GetAsync($"/api/cats/{survivorCatId}");
        Assert.Equal(survivorId, (await survivorCat.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("ownerId").GetGuid());

        var movedCat = await adminClient.GetAsync($"/api/cats/{duplicateCatId}");
        Assert.Equal(survivorId, (await movedCat.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("ownerId").GetGuid());

        var duplicateStatus = await duplicateClient.GetAsync("/api/auth/session");
        Assert.Equal(HttpStatusCode.Unauthorized, duplicateStatus.StatusCode);
    }

    // I-I3
    [Fact]
    public async Task ReSendingTheSameMerge_ReturnsIsReapplicationTrue_StateUnchanged()
    {
        using var registerClient = CreateClient();
        var (survivorId, survivorMobile, survivorPassword) = await RegisterCustomerAsync(registerClient);
        var (duplicateId, duplicateMobile, duplicatePassword) = await RegisterCustomerAsync(registerClient);

        var sharedNumber = NewMobile();
        await AddSharedAdditionalNumberAsync(survivorMobile, survivorPassword, sharedNumber);
        await AddSharedAdditionalNumberAsync(duplicateMobile, duplicatePassword, sharedNumber);

        using var adminClient = await AdministratorClientAsync();
        var first = await adminClient.PostAsJsonAsync($"/api/staff/customers/{duplicateId}/merge", new
        {
            survivingCustomerId = survivorId, reason = "First application"
        }, Json);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // A cat registered on the duplicate id AFTER the first merge -- the documented recovery
        // path (ADR-039) is that re-sending the identical command moves whatever is left. Seeded
        // directly through the repository (SeedCatOwnedByAsync): CODE_REVIEW gate MEDIUM (third
        // pass) now makes the real HTTP registration endpoint reject a non-Active owner, so it can
        // no longer stand in for "a cat the first merge's reassignment loop had not yet reached" --
        // this now seeds that state directly instead, which is what the reassignment handler
        // (OwnerId-only, never customer Status) actually looks for on resend.
        var lateCatId = await SeedCatOwnedByAsync(duplicateId, "Merge Test Cat (Late)");

        var reSend = await adminClient.PostAsJsonAsync($"/api/staff/customers/{duplicateId}/merge", new
        {
            survivingCustomerId = survivorId, reason = "Re-sent, same pair"
        }, Json);
        Assert.Equal(HttpStatusCode.OK, reSend.StatusCode);
        var reSendBody = await reSend.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.True(reSendBody.GetProperty("isReapplication").GetBoolean());

        var lateCat = await adminClient.GetAsync($"/api/cats/{lateCatId}");
        Assert.Equal(survivorId, (await lateCat.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("ownerId").GetGuid());
    }

    // I-I4
    [Fact]
    public async Task Merge_TwoUnrelatedCustomers_Returns400CustomersNotDuplicates()
    {
        using var registerClient = CreateClient();
        var (survivorId, _, _) = await RegisterCustomerAsync(registerClient);
        var (unrelatedId, _, _) = await RegisterCustomerAsync(registerClient);

        using var adminClient = await AdministratorClientAsync();
        var merge = await adminClient.PostAsJsonAsync($"/api/staff/customers/{unrelatedId}/merge", new
        {
            survivingCustomerId = survivorId, reason = "Attempted merge with no shared identifier"
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, merge.StatusCode);
        Assert.Contains("CustomersNotDuplicates", await merge.Content.ReadAsStringAsync());
    }

    // I-I5
    [Fact]
    public async Task Merge_AsReception_Returns403()
    {
        using var registerClient = CreateClient();
        var (survivorId, survivorMobile, survivorPassword) = await RegisterCustomerAsync(registerClient);
        var (duplicateId, duplicateMobile, duplicatePassword) = await RegisterCustomerAsync(registerClient);

        var sharedNumber = NewMobile();
        await AddSharedAdditionalNumberAsync(survivorMobile, survivorPassword, sharedNumber);
        await AddSharedAdditionalNumberAsync(duplicateMobile, duplicatePassword, sharedNumber);

        using var receptionClient = await ReceptionClientAsync();
        var merge = await receptionClient.PostAsJsonAsync($"/api/staff/customers/{duplicateId}/merge", new
        {
            survivingCustomerId = survivorId, reason = "Reception should not be able to do this"
        }, Json);

        Assert.Equal(HttpStatusCode.Forbidden, merge.StatusCode);
    }

    // Integrator fix 2026-09-26 (HO-ADM-001-SECURITY's carried line: "HO-ACC-001:
    // GET /api/staff-accounts?pageNumber=2147483647 -> 500 (the Skip overflow)"; this sibling
    // endpoint shares the exact same gap): GET /api/staff/customers/duplicates applied pageSize
    // with no cap and pageNumber with no cap either -- (pageNumber-1)*pageSize can overflow int
    // (Skip overflow), producing a negative OFFSET Postgres rejects as an uncaught 500. Now capped
    // the same way AdminLeaveRequestsController already is, via the shared PagingRules.
    [Fact]
    public async Task GetDuplicates_PageNumberAtIntMaxValue_Returns400_Never500()
    {
        using var adminClient = await AdministratorClientAsync();

        var response = await adminClient.GetAsync("/api/staff/customers/duplicates?pageNumber=2147483647&pageSize=20");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetDuplicates_PageSizeAtIntMaxValue_Returns400_Never500()
    {
        using var adminClient = await AdministratorClientAsync();

        var response = await adminClient.GetAsync("/api/staff/customers/duplicates?pageNumber=1&pageSize=2147483647");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetDuplicates_PageSizeSkipOverflowCombination_Returns400_Never500()
    {
        using var adminClient = await AdministratorClientAsync();

        // pageNumber=3, pageSize=1_500_000_000 -> Skip = (3-1)*1_500_000_000 overflows int negative.
        var response = await adminClient.GetAsync("/api/staff/customers/duplicates?pageNumber=3&pageSize=1500000000");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // CODE_REVIEW gate MEDIUM (2026-09-26, integrator): GetDuplicateCustomerGroupsQueryHandler had
    // no ORDER BY, so with paging now capped at PagingRules.MaxPageSize (100) a group could fall off
    // page 1 once more than 100 groups accumulate in this shared, never-truncated database (§16.1),
    // and two separate page fetches could return the same group twice or skip one entirely. Now
    // ordered by each group's newest member's registration time (descending) with a stable id
    // tiebreak. This test proves two things without depending on this test's own groups being the
    // ONLY duplicate groups in the shared database (other tests may hold that title concurrently):
    // (1) a group created strictly later ranks strictly ahead of one created earlier, somewhere in
    // the full ordered list; (2) splitting that same list into two pageSize=1 pages reproduces
    // exactly the same order and membership as one pageSize=2 page, with no overlap between the two
    // pages -- i.e. paging is internally stable.
    [Fact]
    public async Task GetDuplicates_NewerGroupRanksAheadOfOlderGroup_AndPagingIsStableAcrossTwoPages()
    {
        using var registerClient = CreateClient();

        var (olderAId, olderAMobile, olderAPassword) = await RegisterCustomerAsync(registerClient);
        var (olderBId, olderBMobile, olderBPassword) = await RegisterCustomerAsync(registerClient);
        var olderSharedNumber = NewMobile();
        await AddSharedAdditionalNumberAsync(olderAMobile, olderAPassword, olderSharedNumber);
        await AddSharedAdditionalNumberAsync(olderBMobile, olderBPassword, olderSharedNumber);

        var (newerAId, newerAMobile, newerAPassword) = await RegisterCustomerAsync(registerClient);
        var (newerBId, newerBMobile, newerBPassword) = await RegisterCustomerAsync(registerClient);
        var newerSharedNumber = NewMobile();
        await AddSharedAdditionalNumberAsync(newerAMobile, newerAPassword, newerSharedNumber);
        await AddSharedAdditionalNumberAsync(newerBMobile, newerBPassword, newerSharedNumber);

        using var adminClient = await AdministratorClientAsync();

        // (1) Ordering: the group created LAST must rank strictly ahead of the one created FIRST,
        // wherever each lands in the full (capped) result set.
        var full = await adminClient.GetAsync("/api/staff/customers/duplicates?pageSize=100");
        Assert.Equal(HttpStatusCode.OK, full.StatusCode);
        var fullItems = (await full.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("items").EnumerateArray().ToList();

        var olderIndex = fullItems.FindIndex(g => g.GetProperty("members").EnumerateArray()
            .Any(m => m.GetProperty("customerId").GetGuid() == olderAId));
        var newerIndex = fullItems.FindIndex(g => g.GetProperty("members").EnumerateArray()
            .Any(m => m.GetProperty("customerId").GetGuid() == newerAId));

        Assert.True(olderIndex >= 0, "Expected the older group to be present.");
        Assert.True(newerIndex >= 0, "Expected the newer group to be present.");
        Assert.True(newerIndex < olderIndex, "Expected the newer group to rank ahead of the older group.");

        // (2) Stable paging: splitting the same two-item window into two pageSize=1 pages must
        // reproduce the same order and membership as fetching both in one pageSize=2 page, with no
        // overlap between the two separately-fetched pages.
        var combined = await adminClient.GetAsync("/api/staff/customers/duplicates?pageNumber=1&pageSize=2");
        var page1 = await adminClient.GetAsync("/api/staff/customers/duplicates?pageNumber=1&pageSize=1");
        var page2 = await adminClient.GetAsync("/api/staff/customers/duplicates?pageNumber=2&pageSize=1");
        Assert.Equal(HttpStatusCode.OK, combined.StatusCode);
        Assert.Equal(HttpStatusCode.OK, page1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, page2.StatusCode);

        var combinedItems = (await combined.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("items").EnumerateArray().ToList();
        var page1Items = (await page1.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("items").EnumerateArray().ToList();
        var page2Items = (await page2.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("items").EnumerateArray().ToList();

        Assert.Equal(2, combinedItems.Count);
        Assert.Single(page1Items);
        Assert.Single(page2Items);

        HashSet<Guid> MemberIds(JsonElement group) => group.GetProperty("members").EnumerateArray()
            .Select(m => m.GetProperty("customerId").GetGuid()).ToHashSet();

        Assert.Equal(MemberIds(combinedItems[0]), MemberIds(page1Items[0]));
        Assert.Equal(MemberIds(combinedItems[1]), MemberIds(page2Items[0]));
        Assert.Empty(MemberIds(page1Items[0]).Intersect(MemberIds(page2Items[0])));
    }
}
