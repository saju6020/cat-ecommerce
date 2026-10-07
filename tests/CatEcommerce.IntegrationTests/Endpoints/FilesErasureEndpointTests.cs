using CatEcommerce.Domain.Aggregates.StoredFile;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Configuration;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// TEST gate (2026-09-27): no persisted test exercised either <c>FilesPersonalDataEraser</c>
/// (ARCHITECTURE HIGH, C-ALL-2) or <see cref="IStoredFileEraser"/>'s own <c>LegalHold</c> backstop
/// (ARCHITECTURE HIGH, first pass) before this gate -- both were verified only by the SECURITY
/// gate's own throwaway probe, kept outside the repository. <c>LegalHold</c> has no public domain
/// setter in this handover (§8.1: "Set only by HO-XCT-001, not built here" -- always <c>false</c>
/// out of <c>Register</c>), so the one test here that needs a held row sets it with a direct SQL
/// update, the same technique <c>LegacyCsrfTokenHashTests</c> already uses in this suite for a
/// state no domain API can produce.
/// </summary>
public sealed class FilesErasureEndpointTests(FilesApiFactory factory) : FilesIntegrationTestBase(factory)
{
    private static async Task<(Guid FileId, string StorageKey)> SeedFileAsync(
        IFileStore fileStore,
        IAggregateRootRepository<StoredFileAggregate> fileRepository,
        Guid uploadedBy,
        DateOnly? retainUntil = null)
    {
        var fileId = Guid.NewGuid();
        var put = await fileStore.PutAsync(new FilePutRequest(
            fileId, FilePurpose.CatProfilePhoto, new MemoryStream("x"u8.ToArray()), "image/png", 1, new string('a', 64)),
            CancellationToken.None);
        Assert.True(put.IsSuccess);

        var aggregate = new StoredFileAggregate(fileId);
        aggregate.Register(new StoredFileRegistrationDto(
            fileId, FilePurpose.CatProfilePhoto, "image/png", 1, new string('a', 64),
            "cat.png", put.Value!, uploadedBy, DateTimeOffset.UtcNow, "DevelopmentNoOp", retainUntil,
            new UserContext(uploadedBy, Guid.NewGuid()), Guid.NewGuid()));
        await fileRepository.SaveAsync(aggregate, EventPublishOption.InMemoryPublishOnly);

        return (fileId, put.Value!);
    }

    // Gate-fix 2026-09-27 (ARCHITECTURE HIGH, C-ALL-2): a customer's plain file is redacted
    // (UploadedBy cleared, name replaced); a file under a future RetainUntil is held, not erased;
    // another customer's file is untouched.
    [Fact]
    public async Task FilesPersonalDataEraser_RedactsAPlainFile_ButHoldsAFutureRetainFile_AndLeavesOtherCustomersFilesAlone()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var fileStore = scope.ServiceProvider.GetRequiredService<IFileStore>();
        var fileRepository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<StoredFileAggregate>>();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
        var eraser = scope.ServiceProvider.GetServices<IPersonalDataEraser>().Single(e => e.ContextKey == "files");

        var customerId = Guid.NewGuid();
        var otherCustomerId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var (plainFileId, _) = await SeedFileAsync(fileStore, fileRepository, customerId);
        var (heldFileId, _) = await SeedFileAsync(fileStore, fileRepository, customerId, retainUntil: today.AddDays(30));
        var (otherCustomerFileId, _) = await SeedFileAsync(fileStore, fileRepository, otherCustomerId);

        var outcome = await eraser.EraseAsync(customerId, DateTimeOffset.UtcNow, new UserContext(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        var held = Assert.IsType<ErasureOutcome.Held>(outcome);
        Assert.Equal(today.AddDays(30), held.Until);

        var plainRow = await db.StoredFiles.AsNoTracking().FirstAsync(f => f.Id == plainFileId);
        Assert.Equal(Guid.Empty, plainRow.UploadedBy);
        Assert.Equal("[redacted]", plainRow.OriginalFileName);

        var heldRow = await db.StoredFiles.AsNoTracking().FirstAsync(f => f.Id == heldFileId);
        Assert.Equal(customerId, heldRow.UploadedBy); // untouched -- still held
        Assert.NotEqual("[redacted]", heldRow.OriginalFileName);

        var otherRow = await db.StoredFiles.AsNoTracking().FirstAsync(f => f.Id == otherCustomerFileId);
        Assert.Equal(otherCustomerId, otherRow.UploadedBy);
        Assert.NotEqual("[redacted]", otherRow.OriginalFileName);

        // Idempotent (contract rule 1): a second call after everything erasable is already
        // redacted still reports Held (the future-retain file remains), and does not throw or
        // re-redact anything.
        var secondOutcome = await eraser.EraseAsync(customerId, DateTimeOffset.UtcNow, new UserContext(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);
        Assert.IsType<ErasureOutcome.Held>(secondOutcome);
    }

    [Fact]
    public async Task FilesPersonalDataEraser_ForACustomerWithNoFiles_ReturnsCompleted()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var eraser = scope.ServiceProvider.GetServices<IPersonalDataEraser>().Single(e => e.ContextKey == "files");

        var outcome = await eraser.EraseAsync(Guid.NewGuid(), DateTimeOffset.UtcNow, new UserContext(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.IsType<ErasureOutcome.Completed>(outcome);
    }

    // Gate-fix 2026-09-27 (ARCHITECTURE HIGH, first pass): LegalHold is enforced INSIDE
    // StoredFileEraser.EraseAsync itself, not only by callers that remember to check first --
    // LegalHold has no public setter in this handover (§8.1), so it is flipped directly in the
    // database, the same technique LegacyCsrfTokenHashTests already uses for a state no domain API
    // can produce.
    [Fact]
    public async Task StoredFileEraser_RefusesAHeldFile_TheObjectAndRowBothRemain()
    {
        string storageKey;
        Guid fileId;

        // Deliberately three SEPARATE scopes (three separate CatEcommerceDbContext instances):
        // EF Core's change tracker resolves a query for an already-tracked id to the IN-MEMORY
        // instance (identity resolution), not a fresh read -- if the seed, the raw SQL flip and
        // the eraser all shared one DbContext, EraseAsync's own query would return the entity as
        // it looked BEFORE the raw SQL update (LegalHold still false, and a now-stale xmin),
        // which the row's real xmin (bumped by the raw UPDATE, a Postgres system column, on every
        // write regardless of how it was made) would then fail an optimistic-concurrency check on
        // -- not the LegalHold refusal this test means to prove at all. Reproduced once while
        // writing this test: with one shared scope, EraseAsync throws
        // DbUpdateConcurrencyException instead of returning false.
        await using (var seedScope = Factory.Services.CreateAsyncScope())
        {
            var fileStore = seedScope.ServiceProvider.GetRequiredService<IFileStore>();
            var fileRepository = seedScope.ServiceProvider.GetRequiredService<IAggregateRootRepository<StoredFileAggregate>>();
            (fileId, storageKey) = await SeedFileAsync(fileStore, fileRepository, Guid.NewGuid());
        }

        await using (var sqlScope = Factory.Services.CreateAsyncScope())
        {
            var db = sqlScope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
            var rows = await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE files.stored_file SET \"LegalHold\" = true WHERE \"Id\" = {fileId}");
            Assert.Equal(1, rows);
        }

        await using (var eraseScope = Factory.Services.CreateAsyncScope())
        {
            var options = eraseScope.ServiceProvider.GetRequiredService<IOptions<FileStorageOptions>>().Value;
            var eraser = eraseScope.ServiceProvider.GetRequiredService<IStoredFileEraser>();

            var erased = await eraser.EraseAsync(fileId, CancellationToken.None);

            Assert.False(erased);
            Assert.True(File.Exists(Path.Combine(options.LocalStoreRoot, storageKey)));
        }

        await using (var verifyScope = Factory.Services.CreateAsyncScope())
        {
            var db = verifyScope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
            Assert.True(await db.StoredFiles.AsNoTracking().AnyAsync(f => f.Id == fileId));
        }
    }

    [Fact]
    public async Task StoredFileEraser_ANonHeldFile_ErasesTheObjectThenTheRow()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var fileStore = scope.ServiceProvider.GetRequiredService<IFileStore>();
        var fileRepository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<StoredFileAggregate>>();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<FileStorageOptions>>().Value;
        var eraser = scope.ServiceProvider.GetRequiredService<IStoredFileEraser>();

        var (fileId, storageKey) = await SeedFileAsync(fileStore, fileRepository, Guid.NewGuid());
        var objectPath = Path.Combine(options.LocalStoreRoot, storageKey);
        Assert.True(File.Exists(objectPath));

        var erased = await eraser.EraseAsync(fileId, CancellationToken.None);

        Assert.True(erased);
        Assert.False(File.Exists(objectPath));
        Assert.False(await db.StoredFiles.AsNoTracking().AnyAsync(f => f.Id == fileId));

        // Idempotent (§8.7): a missing row is not a failure, just nothing left to do.
        var erasedAgain = await eraser.EraseAsync(fileId, CancellationToken.None);
        Assert.False(erasedAgain);
    }
}
