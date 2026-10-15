using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Configuration;
using CatEcommerce.Infrastructure.Files;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-FIL-001 §16, I1-I10.</summary>
public sealed class FilesEndpointTests(FilesApiFactory factory) : FilesIntegrationTestBase(factory)
{
    // I1
    [Fact]
    public async Task Upload_a_clean_png_as_ProductImage_succeeds_and_the_object_and_row_exist()
    {
        using var client = await SignedInStaffClientAsync(StaffRoles.Administrator);

        using var form = BuildUpload(MinimalPng(), "cat.png", "image/png");
        var response = await client.PostAsync("/api/staff/files?purpose=ProductImage", form);

        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var fileId = body.GetProperty("fileId").GetGuid();
        Assert.False(string.IsNullOrEmpty(body.GetProperty("url").GetString()));

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
        var row = await db.StoredFiles.AsNoTracking().FirstOrDefaultAsync(f => f.Id == fileId);
        Assert.NotNull(row);
        Assert.Equal("image/png", row!.ContentType);

        var options = scope.ServiceProvider.GetRequiredService<IOptions<FileStorageOptions>>().Value;
        var objectPath = Path.Combine(options.LocalStoreRoot, row.StorageKey);
        Assert.True(File.Exists(objectPath));

        // The temp file is always cleaned up (§8.2 step 1).
        Assert.Empty(Directory.Exists(options.TempPath) ? Directory.GetFiles(options.TempPath, $"*{fileId:N}*") : []);
    }

    // I2
    [Fact]
    public async Task Upload_the_eicar_string_is_rejected_and_no_object_is_stored()
    {
        using var client = await SignedInStaffClientAsync(StaffRoles.Administrator);

        using var form = BuildUpload(System.Text.Encoding.ASCII.GetBytes(EicarString), "report.csv", "text/csv");
        var response = await client.PostAsync("/api/staff/files?purpose=ReportExport", form);

        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal(BusinessViolationCodes.FileRejected, body.GetProperty("errors")[0].GetProperty("errorCode").GetString());

        await using var scope = Factory.Services.CreateAsyncScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<FileStorageOptions>>().Value;
        // Nothing new was written under the local store root for this purpose in this run beyond
        // what other tests already wrote -- assert no ReportExport object exists whose bytes are
        // the EICAR string, rather than asserting emptiness (§16.1 rule 2).
        var reportExportDir = Path.Combine(options.LocalStoreRoot, "ReportExport");
        if (Directory.Exists(reportExportDir))
        {
            foreach (var file in Directory.GetFiles(reportExportDir, "*", SearchOption.AllDirectories))
            {
                var bytes = await File.ReadAllBytesAsync(file);
                Assert.DoesNotContain(EicarString, System.Text.Encoding.ASCII.GetString(bytes), StringComparison.Ordinal);
            }
        }
    }

    // Gate-fix 2026-09-27 (SECURITY LOW, second pass): the SECURITY gate's own probe (360 bytes of
    // benign CSV followed by an HTA/script payload -> 201 against the old header-only deny-list).
    [Fact]
    public async Task Upload_csv_with_an_hta_payload_after_benign_csv_content_is_rejected()
    {
        using var client = await SignedInStaffClientAsync(StaffRoles.Administrator);
        var benignPrefix = string.Concat(Enumerable.Repeat("name,age\nWhiskers,3\n", 18)); // > 360 bytes
        var content = System.Text.Encoding.ASCII.GetBytes(benignPrefix + "<HTA:APPLICATION/><script language=vbscript>msgbox 1</script>");

        using var form = BuildUpload(content, "report.csv", "text/csv");
        var response = await client.PostAsync("/api/staff/files?purpose=ReportExport", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal(BusinessViolationCodes.FileContentMismatch, body.GetProperty("errors")[0].GetProperty("errorCode").GetString());
    }

    // I3
    [Fact]
    public async Task Upload_when_the_scanner_is_unavailable_is_refused_not_stored_unscanned()
    {
        using var unavailableFactory = Factory.WithUnavailableScanner();
        using var client = unavailableFactory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost/"), HandleCookies = true });
        var (_, mobile, password) = await SeedStaffAsync(StaffRoles.Administrator);
        var signIn = await client.PostAsJsonAsync("/api/staff/auth/sign-in", new { identifier = mobile, password }, Json);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        client.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookieValue(signIn, "pc_csrf"));

        using var form = BuildUpload(MinimalPng(), "cat.png", "image/png");
        var response = await client.PostAsync("/api/staff/files?purpose=ProductImage", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal(BusinessViolationCodes.FileScanUnavailable, body.GetProperty("errors")[0].GetProperty("errorCode").GetString());
    }

    // U4, at the streaming pipeline (integration) level -- only the aggregate's own defence-in-depth
    // re-check (Register) had a persisted test before this TEST gate; nothing proved
    // ProcessFileSectionAsync's own per-chunk cap enforcement returns 400 over real HTTP.
    [Fact]
    public async Task Upload_one_byte_over_the_purpose_cap_is_refused_as_FileTooLarge()
    {
        using var client = await SignedInStaffClientAsync(StaffRoles.Administrator);
        var cap = CatEcommerce.Shared.Common.Constants.FileRules.Find(CatEcommerce.Shared.Common.Enums.FilePurpose.ProductImage)!.MaxSizeInBytes;

        var content = new byte[cap + 1];
        MinimalPng().CopyTo(content, 0);

        using var form = BuildUpload(content, "cat.png", "image/png");
        var response = await client.PostAsync("/api/staff/files?purpose=ProductImage", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal(BusinessViolationCodes.FileTooLarge, body.GetProperty("errors")[0].GetProperty("errorCode").GetString());
        // Temp-file cleanup on this path is already covered by I1/I2/I3's own assertions; the
        // upload's own fileId is never returned to the caller on this refusal, and TempPath is
        // shared across concurrently-running tests in this factory (§16.1 rule 2), so this test's
        // own focus is the violation code the streaming per-chunk cap check returns, not cleanup.
    }

    // I4
    [Fact]
    public async Task When_the_command_save_fails_the_object_is_deleted_again_no_orphan()
    {
        using var failingFactory = Factory.WithFailingRegistration();
        using var client = failingFactory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost/"), HandleCookies = true });
        var (_, mobile, password) = await SeedStaffAsync(StaffRoles.Administrator);
        var signIn = await client.PostAsJsonAsync("/api/staff/auth/sign-in", new { identifier = mobile, password }, Json);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        client.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookieValue(signIn, "pc_csrf"));

        using var form = BuildUpload(MinimalPng(), "cat.png", "image/png");
        var response = await client.PostAsync("/api/staff/files?purpose=ProductImage", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // The object was put, then the command failed, then it must have been deleted again --
        // verified by checking that the LocalFileStore root has grown no orphaned ProductImage
        // objects: every object still on disk for ProductImage must have a matching DB row.
        await using var scope = failingFactory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<FileStorageOptions>>().Value;
        var productImageDir = Path.Combine(options.LocalStoreRoot, "ProductImage");
        if (Directory.Exists(productImageDir))
        {
            var knownKeys = await db.StoredFiles.AsNoTracking()
                .Where(f => f.Purpose == CatEcommerce.Shared.Common.Enums.FilePurpose.ProductImage)
                .Select(f => f.StorageKey)
                .ToListAsync();

            foreach (var objectFile in Directory.GetFiles(productImageDir, "*", SearchOption.AllDirectories))
            {
                var relativeKey = Path.GetRelativePath(options.LocalStoreRoot, objectFile).Replace('\\', '/');
                Assert.Contains(relativeKey, knownKeys);
            }
        }
    }

    // Gate-fix 2026-09-27 (CODE_REVIEW LOW, third pass): the compensating delete on a THROWN save
    // must not be skippable by a client abort racing the same cancellation -- proves
    // FilesController passes CancellationToken.None to fileStore.DeleteAsync in that catch block,
    // not the ambient request token. CancellationToken.None has CanBeCanceled == false, which the
    // ambient (real, ASP.NET Core-issued) request token never does, whether or not it was ever
    // actually cancelled -- so this is a deterministic, non-racy way to prove which one was passed.
    [Fact]
    public async Task When_the_save_throws_the_compensating_delete_uses_CancellationToken_None()
    {
        var (throwingFactory, spy) = Factory.WithThrowingRegistrationAndDeleteTokenSpy();
        using var client = throwingFactory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost/"), HandleCookies = true
        });
        var (_, mobile, password) = await SeedStaffAsync(StaffRoles.Administrator);
        var signIn = await client.PostAsJsonAsync("/api/staff/auth/sign-in", new { identifier = mobile, password }, Json);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        client.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookieValue(signIn, "pc_csrf"));

        using var form = BuildUpload(MinimalPng(), "cat.png", "image/png");
        var response = await client.PostAsync("/api/staff/files?purpose=ProductImage", form);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.NotNull(spy.LastDeleteToken);
        Assert.False(spy.LastDeleteToken!.Value.CanBeCanceled,
            "The compensating delete must run with CancellationToken.None, not the ambient (cancellable) request token.");
    }

    // I5 / I6
    [Fact]
    public async Task Signed_url_works_twice_within_the_window_then_is_refused_after_expiry()
    {
        using var client = await SignedInStaffClientAsync(StaffRoles.Administrator);
        using var form = BuildUpload(MinimalPng(), "cat.png", "image/png");
        var upload = await client.PostAsync("/api/staff/files?purpose=ProductImage", form);
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        var uploadBody = await upload.Content.ReadFromJsonAsync<JsonElement>(Json);
        var url = uploadBody.GetProperty("url").GetString()!;

        var first = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal("nosniff", first.Headers.GetValues("X-Content-Type-Options").First());
        // Gate-fix 2026-09-27 (SECURITY MEDIUM): §8.6 verbatim is "private, no-store". Asserted on
        // the parsed directive flags, not a literal string -- HttpClient's `CacheControlHeaderValue`
        // reparses and reserialises every "known" response header (both via `.CacheControl` and via
        // `GetValues("Cache-Control")`) into its own fixed token order regardless of what the server
        // actually sent on the wire, so no client-visible string ever proves the exact spec text;
        // the two directives being present is the substantive requirement (HTTP directive order
        // carries no meaning, RFC 9111 §5.2).
        Assert.True(first.Headers.CacheControl!.Private);
        Assert.True(first.Headers.CacheControl!.NoStore);

        var second = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        // I6, using the fake-clock factory variant so no real 5-minute wait is needed.
        var (fakeClockFactory, fakeClock) = Factory.WithFakeTimeProvider();
        using var fakeClockClient = fakeClockFactory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost/"), HandleCookies = true });
        var (_, mobile, password) = await SeedStaffAsync(StaffRoles.Administrator);
        var signIn = await fakeClockClient.PostAsJsonAsync("/api/staff/auth/sign-in", new { identifier = mobile, password }, Json);
        fakeClockClient.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookieValue(signIn, "pc_csrf"));

        using var freshForm = BuildUpload(MinimalPng(), "cat.png", "image/png");
        var freshUpload = await fakeClockClient.PostAsync("/api/staff/files?purpose=ProductImage", freshForm);
        var freshUrl = (await freshUpload.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("url").GetString()!;

        fakeClock.Advance(TimeSpan.FromMinutes(6));
        var expired = await fakeClockClient.GetAsync(freshUrl);
        Assert.Equal(HttpStatusCode.Forbidden, expired.StatusCode);
        // "No detail" (§8.6): the body carries nothing that distinguishes tampering from expiry
        // or names the signature/key -- just the empty object FilesController returns.
        Assert.Equal("{}", await expired.Content.ReadAsStringAsync());
    }

    // I7
    [Fact]
    public async Task Dicom_and_zip_are_always_served_as_attachment_even_when_inline_is_requested()
    {
        // HO-EMR-001 (2026-10-07): LabResult is now owned by Consultation -- uploading one needs Visits.WriteNote (Vet), no longer
        // "any staff role" via the test-only fall-back. The ReportExport zip below is still unowned, so the same Vet may upload it.
        using var client = await SignedInStaffClientAsync(StaffRoles.Vet);

        using var dicomForm = BuildUpload(BuildDicomBytes(), "scan.dcm", "application/dicom");
        var dicomUpload = await client.PostAsync("/api/staff/files?purpose=LabResult", dicomForm);
        Assert.Equal(HttpStatusCode.Created, dicomUpload.StatusCode);
        var dicomFileId = (await dicomUpload.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("fileId").GetGuid();

        using var zipForm = BuildUpload([0x50, 0x4B, 0x03, 0x04, 0x00, 0x00], "export.zip", "application/zip");
        var zipUpload = await client.PostAsync("/api/staff/files?purpose=ReportExport", zipForm);
        Assert.Equal(HttpStatusCode.Created, zipUpload.StatusCode);
        var zipFileId = (await zipUpload.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("fileId").GetGuid();

        await using var scope = Factory.Services.CreateAsyncScope();
        var signer = scope.ServiceProvider.GetRequiredService<HmacFileUrlSigner>();

        foreach (var fileId in new[] { dicomFileId, zipFileId })
        {
            var inlineUrl = signer.Sign(fileId, TimeSpan.FromMinutes(5), "inline");
            var response = await client.GetAsync(inlineUrl);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("attachment", response.Content.Headers.ContentDisposition!.ToString(), StringComparison.OrdinalIgnoreCase);
        }
    }

    // I8
    [Fact]
    public async Task Purge_deletes_the_object_first_then_the_row_and_a_second_run_is_a_no_op()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var fileStore = scope.ServiceProvider.GetRequiredService<IFileStore>();
        var fileRepository = scope.ServiceProvider.GetRequiredService<Platform.Infrastructure.Core.Domain.IAggregateRootRepository<CatEcommerce.Domain.Aggregates.StoredFile.StoredFileAggregate>>();
        // One IRetentionPurgeSource is registered per FilePurpose (gate-fix 2026-09-27) -- resolve
        // the one for ProductImage specifically, not GetRequiredService<T>() (which would silently
        // return whichever purpose's source was registered last).
        var purgeSource = scope.ServiceProvider.GetServices<IRetentionPurgeSource>()
            .Single(s => s.SourceKey == $"stored-file:{CatEcommerce.Shared.Common.Enums.FilePurpose.ProductImage.DisplayName}");
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();

        var fileId = Guid.NewGuid();
        var putResult = await fileStore.PutAsync(new CatEcommerce.Domain.Models.FilePutRequest(
            fileId, CatEcommerce.Shared.Common.Enums.FilePurpose.ProductImage, new MemoryStream("x"u8.ToArray()),
            "image/png", 1, new string('a', 64)), CancellationToken.None);
        Assert.True(putResult.IsSuccess);

        var oldUploadedOn = DateTimeOffset.UtcNow.AddYears(-2);
        var file = new CatEcommerce.Domain.Aggregates.StoredFile.StoredFileAggregate(fileId);
        file.Register(new CatEcommerce.Domain.Models.StoredFileRegistrationDto(
            fileId, CatEcommerce.Shared.Common.Enums.FilePurpose.ProductImage, "image/png", 1, new string('a', 64),
            "old.png", putResult.Value!, Guid.NewGuid(), oldUploadedOn, "DevelopmentNoOp", null,
            new Platform.Infrastructure.Common.Security.UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
        await fileRepository.SaveAsync(file, Platform.Infrastructure.Core.Commands.EventPublishOption.InMemoryPublishOnly);

        var options = scope.ServiceProvider.GetRequiredService<IOptions<FileStorageOptions>>().Value;
        var objectPath = Path.Combine(options.LocalStoreRoot, putResult.Value!);
        Assert.True(File.Exists(objectPath));

        var firstPurge = await purgeSource.PurgeAsync(DateTimeOffset.UtcNow, batchSize: 1000, CancellationToken.None);
        Assert.True(firstPurge.Purged >= 1);
        Assert.False(File.Exists(objectPath));
        Assert.False(await db.StoredFiles.AsNoTracking().AnyAsync(f => f.Id == fileId));

        // Idempotent: running the purge again finds this file gone already -- neither purged
        // again nor counted as a failure.
        await purgeSource.PurgeAsync(DateTimeOffset.UtcNow, batchSize: 1000, CancellationToken.None);
        Assert.False(File.Exists(objectPath));
        Assert.Equal(0, await db.StoredFiles.CountAsync(f => f.Id == fileId));
    }

    // Gate-fix 2026-09-27 (CODE_REVIEW LOW, first pass / carried): StoredFilePurgeSource applies
    // the LegalHold/RetainUntil eligibility predicate in the SQL query itself, before
    // Take(batchSize) -- previously applied in memory AFTER the batch was already fetched, so a
    // batch made up mostly of held rows (ordered first by UploadedOn, the oldest-first order the
    // query uses) could do no real purging work even though later, genuinely-eligible rows
    // existed beyond it. I8 only ever exercises one file at a time, so it cannot tell the two
    // orderings apart -- this seeds several held (future-RetainUntil) rows with an EARLIER
    // UploadedOn than several genuinely-eligible rows, then purges with a batch too small to
    // reach the eligible rows AT ALL if the held rows were still being drawn into it first.
    [Fact]
    public async Task Purge_batch_is_not_starved_by_held_rows_ordered_before_eligible_ones()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var fileStore = scope.ServiceProvider.GetRequiredService<IFileStore>();
        var fileRepository = scope.ServiceProvider.GetRequiredService<Platform.Infrastructure.Core.Domain.IAggregateRootRepository<CatEcommerce.Domain.Aggregates.StoredFile.StoredFileAggregate>>();
        var purgeSource = scope.ServiceProvider.GetServices<IRetentionPurgeSource>()
            .Single(s => s.SourceKey == $"stored-file:{CatEcommerce.Shared.Common.Enums.FilePurpose.ProductImage.DisplayName}");
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<FileStorageOptions>>().Value;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        async Task<Guid> SeedAsync(DateTimeOffset uploadedOn, DateOnly? retainUntil)
        {
            var seededId = Guid.NewGuid();
            var put = await fileStore.PutAsync(new CatEcommerce.Domain.Models.FilePutRequest(
                seededId, CatEcommerce.Shared.Common.Enums.FilePurpose.ProductImage, new MemoryStream("x"u8.ToArray()),
                "image/png", 1, new string('a', 64)), CancellationToken.None);
            Assert.True(put.IsSuccess);

            var aggregate = new CatEcommerce.Domain.Aggregates.StoredFile.StoredFileAggregate(seededId);
            aggregate.Register(new CatEcommerce.Domain.Models.StoredFileRegistrationDto(
                seededId, CatEcommerce.Shared.Common.Enums.FilePurpose.ProductImage, "image/png", 1, new string('a', 64),
                "old.png", put.Value!, Guid.NewGuid(), uploadedOn, "DevelopmentNoOp", retainUntil,
                new Platform.Infrastructure.Common.Security.UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
            await fileRepository.SaveAsync(aggregate, Platform.Infrastructure.Core.Commands.EventPublishOption.InMemoryPublishOnly);
            return seededId;
        }

        // Deliberately far in the past and mutually distinct so ordering is stable regardless of
        // whatever ambient ProductImage rows other tests in this shared database have created
        // (§16.1 rule 2 -- never assert emptiness; this test asserts specific ids instead, exactly
        // to stay correct under that shared state).
        var heldIds = new List<Guid>();
        for (var i = 0; i < 5; i++)
            heldIds.Add(await SeedAsync(DateTimeOffset.UtcNow.AddYears(-10).AddMinutes(i), today.AddDays(30)));

        var eligibleIds = new List<Guid>();
        for (var i = 0; i < 3; i++)
            eligibleIds.Add(await SeedAsync(DateTimeOffset.UtcNow.AddYears(-9).AddMinutes(i), null));

        // batchSize equals the held-row count: under the pre-fix in-memory filter, Take(5) would
        // draw exactly the 5 (oldest) held rows and find nothing eligible within them, leaving the
        // 3 genuinely-eligible rows (which sort right after) entirely unexamined.
        var result = await purgeSource.PurgeAsync(DateTimeOffset.UtcNow, batchSize: 5, CancellationToken.None);

        Assert.True(result.Purged >= eligibleIds.Count,
            $"Expected at least the {eligibleIds.Count} seeded eligible rows to be purged despite 5 held rows sorting before them; got Purged={result.Purged}.");

        foreach (var eligibleId in eligibleIds)
            Assert.False(await db.StoredFiles.AsNoTracking().AnyAsync(f => f.Id == eligibleId), $"Eligible file {eligibleId} should have been purged.");

        foreach (var heldId in heldIds)
        {
            var heldRow = await db.StoredFiles.AsNoTracking().FirstOrDefaultAsync(f => f.Id == heldId);
            Assert.NotNull(heldRow);
            Assert.True(File.Exists(Path.Combine(options.LocalStoreRoot, heldRow!.StorageKey)), $"Held file {heldId}'s object should not have been deleted.");
        }
    }

    // FIL backlog row 4: the purge source used to count held files with two separate COUNT
    // statements (all past the cutoff, then the eligible ones, subtracting), which can disagree
    // under concurrent writes. It now reads both from ONE statement. A command interceptor on a
    // purge source built over its own context counts the COUNT statements it issues.
    [Fact]
    public async Task Purge_counts_held_files_with_a_single_statement()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var fileRepository = scope.ServiceProvider.GetRequiredService<Platform.Infrastructure.Core.Domain.IAggregateRootRepository<CatEcommerce.Domain.Aggregates.StoredFile.StoredFileAggregate>>();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        for (var i = 0; i < 3; i++)
        {
            var seededId = Guid.NewGuid();
            var aggregate = new CatEcommerce.Domain.Aggregates.StoredFile.StoredFileAggregate(seededId);
            aggregate.Register(new CatEcommerce.Domain.Models.StoredFileRegistrationDto(
                seededId, CatEcommerce.Shared.Common.Enums.FilePurpose.ProductImage, "image/png", 1, new string('a', 64),
                "held.png", $"held/{seededId}", Guid.NewGuid(), DateTimeOffset.UtcNow.AddYears(-10), "DevelopmentNoOp",
                today.AddYears(10),
                new Platform.Infrastructure.Common.Security.UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
            await fileRepository.SaveAsync(aggregate, Platform.Infrastructure.Core.Commands.EventPublishOption.InMemoryPublishOnly);
        }

        var interceptor = new CommandTextRecorder();
        var baseOptions = scope.ServiceProvider.GetRequiredService<DbContextOptions<CatEcommerceDbContext>>();
        var options = new DbContextOptionsBuilder<CatEcommerceDbContext>(baseOptions).AddInterceptors(interceptor).Options;
        await using var countingDb = new CatEcommerceDbContext(options);
        var source = new StoredFilePurgeSource(
            CatEcommerce.Shared.Common.Enums.FilePurpose.ProductImage,
            countingDb,
            scope.ServiceProvider.GetRequiredService<IStoredFileEraser>(),
            TimeProvider.System,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<StoredFilePurgeSource>.Instance);

        // batchSize 1 keeps the purge itself small; only the held count is under test.
        var result = await source.PurgeAsync(DateTimeOffset.UtcNow, batchSize: 1, CancellationToken.None);

        Assert.True(result.Held >= 3, $"Expected the 3 seeded held files to be counted; got Held={result.Held}.");
        var countStatements = interceptor.Commands.Count(c => c.Contains("COUNT(", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, countStatements);
    }

    private sealed class CommandTextRecorder : Microsoft.EntityFrameworkCore.Diagnostics.DbCommandInterceptor
    {
        public List<string> Commands { get; } = [];

        public override async ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<System.Data.Common.DbDataReader>> ReaderExecutingAsync(
            System.Data.Common.DbCommand command,
            Microsoft.EntityFrameworkCore.Diagnostics.CommandEventData eventData,
            Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<System.Data.Common.DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Commands.Add(command.CommandText);
            return await base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    // Gate-fix 2026-09-27 (TEST gate, U11 weak-test finding): the shared
    // StoredFileAggregate.IsEligibleForPurge expression, tested here against
    // GetFilesForPurgeQueryHandler (the OTHER real consumer, alongside StoredFilePurgeSource,
    // which the two tests above already exercise for the RetainUntil branches). LegalHold has no
    // public setter anywhere in this slice (HO-XCT-001, not built here, is the only intended
    // writer) -- one row's LegalHold is flipped with a direct, test-only SQL UPDATE, the only way
    // to exercise that branch at all before HO-XCT-001 lands.
    [Fact]
    public async Task GetFilesForPurgeQuery_excludes_legal_hold_and_future_retain_until_includes_past_and_null()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var fileStore = scope.ServiceProvider.GetRequiredService<IFileStore>();
        var fileRepository = scope.ServiceProvider.GetRequiredService<Platform.Infrastructure.Core.Domain.IAggregateRootRepository<CatEcommerce.Domain.Aggregates.StoredFile.StoredFileAggregate>>();
        var handler = scope.ServiceProvider.GetRequiredService<Platform.Infrastructure.Core.Queries.IQueryHandlerAsync<
            CatEcommerce.Read.Queries.StoredFile.GetFilesForPurgeQuery, IReadOnlyList<CatEcommerce.Read.ViewModels.PurgeEligibleFileViewModel>>>();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();

        // Deliberately far in the past and mutually distinct, exactly like the purge-batch test
        // above, so this is correct regardless of whatever ambient rows the shared database holds
        // (§16.1 rule 2).
        var uploadedOn = DateTimeOffset.UtcNow.AddYears(-11);

        async Task<Guid> SeedAsync(DateOnly? retainUntil)
        {
            var seededId = Guid.NewGuid();
            var put = await fileStore.PutAsync(new CatEcommerce.Domain.Models.FilePutRequest(
                seededId, CatEcommerce.Shared.Common.Enums.FilePurpose.ProductImage, new MemoryStream("x"u8.ToArray()),
                "image/png", 1, new string('a', 64)), CancellationToken.None);
            Assert.True(put.IsSuccess);

            var aggregate = new CatEcommerce.Domain.Aggregates.StoredFile.StoredFileAggregate(seededId);
            aggregate.Register(new CatEcommerce.Domain.Models.StoredFileRegistrationDto(
                seededId, CatEcommerce.Shared.Common.Enums.FilePurpose.ProductImage, "image/png", 1, new string('a', 64),
                "old.png", put.Value!, Guid.NewGuid(), uploadedOn, "DevelopmentNoOp", retainUntil,
                new Platform.Infrastructure.Common.Security.UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
            await fileRepository.SaveAsync(aggregate, Platform.Infrastructure.Core.Commands.EventPublishOption.InMemoryPublishOnly);
            return seededId;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var legalHoldId = await SeedAsync(retainUntil: null); // eligible by RetainUntil alone -- LegalHold is what must exclude it
        var futureRetainId = await SeedAsync(retainUntil: today.AddDays(30));
        var pastRetainId = await SeedAsync(retainUntil: today.AddDays(-1));
        var noRetainId = await SeedAsync(retainUntil: null);

        // LegalHold has no public mutator anywhere in this codebase (by design -- HO-XCT-001, not
        // built here, is the only intended writer). A direct SQL UPDATE is the only way to put a
        // row in that state before then.
        var rowsUpdated = await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE files.stored_file SET \"LegalHold\" = true WHERE \"Id\" = {legalHoldId}");
        Assert.Equal(1, rowsUpdated);

        var response = await handler.HandleAsync(new CatEcommerce.Read.Queries.StoredFile.GetFilesForPurgeQuery
        {
            Cutoff = DateTimeOffset.UtcNow
        });

        var resultIds = response.Result!.Select(r => r.FileId).ToHashSet();

        try
        {
            Assert.DoesNotContain(legalHoldId, resultIds);
            Assert.DoesNotContain(futureRetainId, resultIds);
            Assert.Contains(pastRetainId, resultIds);
            Assert.Contains(noRetainId, resultIds);
        }
        finally
        {
            // This test only ever exercises the READ side (GetFilesForPurgeQueryHandler) -- unlike
            // Purge_batch_is_not_starved_by_held_rows_ordered_before_eligible_ones above, nothing
            // here ever calls the real purge/eraser to remove these rows. Left behind, these four
            // rows (uploaded 11 years in the past, two of them genuinely eligible) would sort before
            // -- and starve batchSize-limited purge batches of -- that other test's own, later-dated
            // seeded rows in this shared database (confirmed: this cleanup's absence was observed to
            // make that other test fail intermittently once both had run in the same suite).
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM files.stored_file WHERE \"Id\" IN ({legalHoldId}, {futureRetainId}, {pastRetainId}, {noRetainId})");
        }
    }

    // I9
    [Fact]
    public async Task Reading_a_file_whose_object_was_deleted_out_of_band_is_a_logged_error_not_a_silent_404()
    {
        var (capturingFactory, logs) = Factory.WithCapturedLogs();
        using var client = capturingFactory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost/"), HandleCookies = true
        });
        var (_, mobile, password) = await SeedStaffAsync(StaffRoles.Administrator);
        var signIn = await client.PostAsJsonAsync("/api/staff/auth/sign-in", new { identifier = mobile, password }, Json);
        client.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookieValue(signIn, "pc_csrf"));

        using var form = BuildUpload(MinimalPng(), "cat.png", "image/png");
        var upload = await client.PostAsync("/api/staff/files?purpose=ProductImage", form);
        var uploadBody = await upload.Content.ReadFromJsonAsync<JsonElement>(Json);
        var url = uploadBody.GetProperty("url").GetString()!;

        await using (var scope = capturingFactory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
            var options = scope.ServiceProvider.GetRequiredService<IOptions<FileStorageOptions>>().Value;
            var fileId = uploadBody.GetProperty("fileId").GetGuid();
            var row = await db.StoredFiles.AsNoTracking().FirstAsync(f => f.Id == fileId);
            File.Delete(Path.Combine(options.LocalStoreRoot, row.StorageKey));
        }

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        // Not a silent 404 -- LocalFileStore.OpenReadAsync calls ILogger.LogError for exactly this
        // case (source-verified); CapturingLoggerProvider does not preserve LogLevel in its string
        // form, so this asserts the message itself was actually logged, not merely that the source
        // calls LogError.
        Assert.Contains(logs, l => l.Contains("CatEcommerce.Infrastructure.Files.LocalFileStore", StringComparison.Ordinal)
                                    && l.Contains("missing", StringComparison.OrdinalIgnoreCase));
    }

    // I10 -- see HO-FIL-001 report for the documented deviation from a literal 50 MB run.
    //
    // Gate-fix 2026-09-27 (CODE_REVIEW LOW), abandoned after two empirical failures under the full
    // suite's real, parallel execution:
    // 1. One upload's absolute `GC.GetTotalMemory` delta against a flat 150 MB floor -- loose
    //    enough that the very regression it claims to guard against (ProcessFileSectionAsync
    //    buffering a whole ~20 MB file instead of its fixed 80 KB chunks) would not have crossed
    //    it.
    // 2. The MARGINAL `GC.GetTotalMemory` delta of a large upload against a small one, back to
    //    back, on the theory that this suite's background allocation noise affects both
    //    measurements equally and cancels out. It did not: xUnit runs other test collections
    //    concurrently by default, so a whole-PROCESS live-heap snapshot moved by tens of MB, in
    //    either direction, between the two measurements (a real run recorded a *negative* delta on
    //    both uploads).
    // 3. `GC.GetAllocatedBytesForCurrentThread()` (a per-thread monotonic allocation counter,
    //    immune to other threads' GC activity) on the theory that the calling thread would stay
    //    fixed across the `await`. It did not: ASP.NET Core's TestServer resumes the continuation
    //    after `await client.PostAsync(...)` on whatever thread-pool thread is free, which is
    //    reused by unrelated concurrent tests between this method's two snapshots -- the "before"
    //    and "after" reads are not reliably the same thread's counter at all (a real run recorded
    //    a 128 MB "marginal cost" for an 18 MB size difference, with the two absolute readings
    //    themselves in the hundreds of MB -- each a whole thread-pool thread's lifetime total, not
    //    this test's own allocation).
    //
    // A reliable per-request allocation measurement in a real, concurrently-executing ASP.NET Core
    // host needs an out-of-process profiler (dotnet-trace/EventPipe counters), not a GC.* call in
    // the test itself -- out of proportion for this one heuristic. This is therefore a smoke test
    // (a large upload succeeds and is fully readable back) plus source review as the actual
    // guarantee: the read loop in ProcessFileSectionAsync never allocates a buffer larger than
    // 80 KB regardless of file size (reviewed, not re-asserted at runtime here).
    [Fact]
    public async Task A_large_upload_succeeds_and_is_served_back_intact()
    {
        using var client = await SignedInStaffClientAsync(StaffRoles.Administrator);
        var largeContent = BuildLargeZipBytes(20 * 1024 * 1024); // 20 MB -- see report for rationale

        using var form = BuildUpload(largeContent, "export.zip", "application/zip");
        var response = await client.PostAsync("/api/staff/files?purpose=ReportExport", form);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var uploadBody = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var url = uploadBody.GetProperty("url").GetString()!;

        var fetch = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, fetch.StatusCode);
        var servedBytes = await fetch.Content.ReadAsByteArrayAsync();
        Assert.Equal(largeContent.Length, servedBytes.Length);
        Assert.True(largeContent.AsSpan().SequenceEqual(servedBytes));
    }

    // Gate-fix 2026-09-27 (ARCHITECTURE MEDIUM, second pass): neither rate-limit policy had a test
    // proving it actually runs -- matches RateLimitingTests' own tight-limit-factory pattern.
    [Fact]
    public async Task Upload_OverLimit_ThirdReturns429()
    {
        using var client = Factory.WithTightFileRateLimits().CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost/"), HandleCookies = true
        });
        var (_, mobile, password) = await SeedStaffAsync(StaffRoles.Administrator);
        var signIn = await client.PostAsJsonAsync("/api/staff/auth/sign-in", new { identifier = mobile, password }, Json);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        client.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookieValue(signIn, "pc_csrf"));

        HttpResponseMessage? last = null;
        for (var i = 0; i < 3; i++)
        {
            using var form = BuildUpload(MinimalPng(), "cat.png", "image/png");
            last = await client.PostAsync("/api/staff/files?purpose=ProductImage", form);
        }

        Assert.Equal((HttpStatusCode)429, last!.StatusCode);
        Assert.True(last.Headers.Contains("Retry-After"));
    }

    [Fact]
    public async Task Serve_OverLimit_ThirdReturns429()
    {
        using var client = Factory.WithTightFileRateLimits().CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost/"), HandleCookies = true
        });
        var (_, mobile, password) = await SeedStaffAsync(StaffRoles.Administrator);
        var signIn = await client.PostAsJsonAsync("/api/staff/auth/sign-in", new { identifier = mobile, password }, Json);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        client.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookieValue(signIn, "pc_csrf"));

        using var form = BuildUpload(MinimalPng(), "cat.png", "image/png");
        var upload = await client.PostAsync("/api/staff/files?purpose=ProductImage", form);
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        var url = (await upload.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("url").GetString()!;

        HttpResponseMessage? last = null;
        for (var i = 0; i < 3; i++)
            last = await client.GetAsync(url);

        Assert.Equal((HttpStatusCode)429, last!.StatusCode);
        Assert.True(last.Headers.Contains("Retry-After"));
    }

    private static byte[] BuildLargeZipBytes(int size)
    {
        var bytes = new byte[size];
        bytes[0] = 0x50; bytes[1] = 0x4B; bytes[2] = 0x03; bytes[3] = 0x04;
        return bytes;
    }

    private static byte[] BuildDicomBytes()
    {
        var header = new byte[140];
        "DICM"u8.CopyTo(header.AsSpan(128));
        return header;
    }
}
