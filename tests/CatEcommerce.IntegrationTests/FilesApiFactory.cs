using CatEcommerce.Application.Commands.StoredFile;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Files;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.IntegrationTests;

/// <summary>
/// HO-FIL-001. Real HTTP through TestServer, against this branch's own <c>catecommerce_fil</c>
/// database (see README's "per-branch database"). <c>FileStorage:UrlSigningKey</c> is set here --
/// outside Development/Testing the host refuses to start without one (§8.6,
/// <c>FileStorageStartupGuardHostedService</c>), and while the guard does not enforce it in
/// Testing, <c>HmacFileUrlSigner</c> still needs a real value to sign/verify with.
/// </summary>
public sealed class FilesApiFactory : CatEcommerceApiFactory
{
    // Gate-fix 2026-09-27 (SECURITY MEDIUM): both FileStorageOptions defaults
    // (%TEMP%\catecommerce-file-store, %TEMP%\catecommerce-file-uploads) are fixed, machine-wide
    // paths -- shared not just across test classes but across every branch's worktree run against
    // this machine. I4 was observed to fail against objects left over from another run against
    // another branch's database. One instance of this factory now gets its own subtree, computed
    // once so every WithWebHostBuilder()-derived factory built from it (WithUnavailableScanner,
    // WithFailingRegistration, WithFakeTimeProvider, WithCapturedLogs) shares it too.
    private readonly string _isolatedRoot =
        Path.Combine(Path.GetTempPath(), "catecommerce-file-store-tests", Guid.NewGuid().ToString("N"));

    protected override void ConfigureAdditionalSettings(IDictionary<string, string?> settings)
    {
        settings["FileStorage:UrlSigningKey"] = "test-environment-signing-key-at-least-32-bytes-long";
        settings["FileStorage:LocalStoreRoot"] = Path.Combine(_isolatedRoot, "store");
        settings["FileStorage:TempPath"] = Path.Combine(_isolatedRoot, "temp");
        settings["DevUser:Enabled"] = "false";
        settings["ErrorVerbosity"] = "ExceptionMessage";

        // Gate-fix 2026-09-27 (ARCHITECTURE MEDIUM, second pass): needed by
        // RegisterAndSignInCustomerAsync (§1 Amended (10)'s customer upload-then-attach test),
        // matching AdministrationApiFactory's identical settings.
        settings["Captcha:TestAnswer"] = "TEST1";
        settings["RateLimiting:CaptchaIssue:PermitLimit"] = "100000";
        settings["RateLimiting:Registration:PermitLimit"] = "100000";
        settings["RateLimiting:SignIn:PermitLimit"] = "100000";
    }

    /// <summary>
    /// Gate-fix 2026-09-27 (SECURITY MEDIUM): <c>IFilePurposeAccessPolicy</c> now fails closed --
    /// a purpose with no registered staff action is refused, not implicitly allowed. §16's own
    /// tests exercise several purposes (<c>ProductImage</c>, <c>LabResult</c>, <c>ReportExport</c>,
    /// ...) chosen for their <c>FileRules</c> content-type/size shape (DICOM, zip, csv, …), not for
    /// any consuming area's own authorisation -- none of those areas (SHP, EMR, RPT) has landed yet,
    /// so none of their purposes has a real mapping in <c>FilePurposeAccess</c>. This test-only
    /// decorator falls back to <c>Actions.Files.Upload</c> (already granted to every staff role,
    /// <c>ActionRoles.FilesRows</c>) for any purpose the REAL policy does not already recognise, so
    /// I1-I10 keep exercising the upload pipeline itself; a purpose the real policy DOES map
    /// (<c>CatProfilePhoto</c>, gated to <c>Cats.EditProfile</c>) still goes through that real,
    /// stricter check first. Never registered outside this test host (see
    /// <see cref="IFilePurposeAccessPolicy"/>'s own doc comment).
    /// </summary>
    protected override void ConfigureAdditionalServices(IServiceCollection services)
    {
        services.AddScoped<IFilePurposeAccessPolicy>(sp =>
            new UnownedPurposeAllowingPolicy(new DefaultFilePurposeAccessPolicy()));
    }

    private sealed class UnownedPurposeAllowingPolicy(IFilePurposeAccessPolicy inner) : IFilePurposeAccessPolicy
    {
        public bool TryGetRequiredStaffAction(FilePurpose purpose, out string action)
        {
            if (inner.TryGetRequiredStaffAction(purpose, out action)) return true;

            action = Actions.Files.Upload;
            return true;
        }

        public bool IsAllowedForCustomer(FilePurpose purpose) => inner.IsAllowedForCustomer(purpose);
    }

    /// <summary>I3: "Scanner container stopped" -- simulated by swapping the registered
    /// <see cref="IMalwareScanner"/> for <see cref="UnconfiguredMalwareScanner"/>, the same
    /// fail-closed adapter selected in Staging/Production when <c>clamd</c> is unreachable/
    /// unconfigured (§8.5). The Testing environment's own scanner (<see cref="EicarTestScanner"/>)
    /// is a deterministic in-process stand-in that never times out on its own, so this is the
    /// faithful way to exercise "the scanner is down" without a real clamd dependency in CI.</summary>
    public WebApplicationFactory<Program> WithUnavailableScanner() => WithWebHostBuilder(builder =>
        builder.ConfigureTestServices(services =>
            services.AddScoped<IMalwareScanner, UnconfiguredMalwareScanner>()));

    /// <summary>I4: "PutAsync succeeds, the command save then fails" -- decorates the real
    /// <see cref="RegisterStoredFileCommand"/> handler with one that always fails, so the test can
    /// assert the compensating object delete (§8.2 step 7) without needing a real database failure.</summary>
    public WebApplicationFactory<Program> WithFailingRegistration() => WithWebHostBuilder(builder =>
        builder.ConfigureTestServices(services =>
            services.AddScoped<ICommandHandlerAsync<RegisterStoredFileCommand>, AlwaysFailingRegisterStoredFileCommandHandler>()));

    /// <summary>I6: a controllable clock so an issued signed URL can be pushed past its own
    /// <c>exp</c> deterministically, without a real 5-minute sleep.</summary>
    public (WebApplicationFactory<Program> Factory, FakeTimeProvider TimeProvider) WithFakeTimeProvider()
    {
        var fakeTimeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var factory = WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(fakeTimeProvider)));

        return (factory, fakeTimeProvider);
    }

    /// <summary>Gate-fix 2026-09-27 (ARCHITECTURE MEDIUM, second pass): a real 429 on each of
    /// FileStorageModule's two rate-limit policies, matching <c>AccountsApiFactory.WithTightRateLimits()</c>'s
    /// pattern -- lowers only these two policies, leaving the production defaults (bound from the
    /// "RateLimiting" config section) untouched for every other test.</summary>
    public WebApplicationFactory<Program> WithTightFileRateLimits() => WithWebHostBuilder(builder =>
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:FileUpload:PermitLimit"] = "2",
                ["RateLimiting:FileUpload:WindowMinutes"] = "10",
                ["RateLimiting:FileServe:PermitLimit"] = "2",
                ["RateLimiting:FileServe:WindowMinutes"] = "1",
            });
        }));

    private sealed class AlwaysFailingRegisterStoredFileCommandHandler : ICommandHandlerAsync<RegisterStoredFileCommand>
    {
        public Task<CommandResponse> HandleAsync(RegisterStoredFileCommand command)
        {
            var validationResponse = new ValidationResponse();
            validationResponse.AddError(
                errorMessage: "Simulated persistence failure (I4).", propertyName: "FileId",
                errorCode: "SimulatedFailure", resourceName: "StoredFile");

            return Task.FromResult(new CommandResponse { IsSuccess = false, ValidationResult = validationResponse });
        }
    }

    /// <summary>Gate-fix 2026-09-27 (CODE_REVIEW LOW, third pass): proves the compensating
    /// <c>fileStore.DeleteAsync</c> calls in <c>FilesController</c> (§8.2 step 7, both when the save
    /// THROWS and when it returns <c>IsSuccess=false</c>) always pass <c>CancellationToken.None</c>,
    /// not the ambient request token -- which matters specifically when the client disconnects while
    /// <c>dispatcher.SendAsync</c> is in flight: by the time either branch runs, the ambient token
    /// may already be cancelled, and <c>CancellationToken.None</c> (unlike the ambient token)
    /// distinctly has <see cref="CancellationToken.CanBeCanceled"/> <c>== false</c> regardless of
    /// whether anything was ever actually cancelled -- an observable, deterministic difference this
    /// test asserts directly, rather than trying to race a real client abort. The registered
    /// <see cref="IFileStore"/> is a decorator that "honours" cancellation (throws if given an
    /// already-cancelled token) precisely because the real <c>LocalFileStore.DeleteAsync</c> used in
    /// Testing does not -- without this decorator, a genuinely-cancelled token reaching the delete
    /// call would be invisible to any test.</summary>
    public (WebApplicationFactory<Program> Factory, DeleteTokenSpy Spy) WithThrowingRegistrationAndDeleteTokenSpy()
    {
        var spy = new DeleteTokenSpy();
        var factory = WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(spy);
                services.AddScoped<ICommandHandlerAsync<RegisterStoredFileCommand>, AlwaysThrowingRegisterStoredFileCommandHandler>();
                services.AddScoped<IFileStore>(sp =>
                    new CancellationHonouringFileStoreDecorator(sp.GetRequiredService<LocalFileStore>(), spy));
            }));

        return (factory, spy);
    }

    public sealed class DeleteTokenSpy
    {
        public CancellationToken? LastDeleteToken { get; set; }
    }

    private sealed class AlwaysThrowingRegisterStoredFileCommandHandler : ICommandHandlerAsync<RegisterStoredFileCommand>
    {
        public Task<CommandResponse> HandleAsync(RegisterStoredFileCommand command) =>
            throw new InvalidOperationException("Simulated persistence failure (test double).");
    }

    private sealed class CancellationHonouringFileStoreDecorator(IFileStore inner, DeleteTokenSpy spy) : IFileStore
    {
        public Task<FileStoreResult<string>> PutAsync(FilePutRequest request, CancellationToken ct) =>
            inner.PutAsync(request, ct);

        public Task<FileStoreResult<Stream>> OpenReadAsync(string storageKey, CancellationToken ct) =>
            inner.OpenReadAsync(storageKey, ct);

        public Task<FileStoreResult<CatEcommerce.Shared.Common.Helpers.Unit>> DeleteAsync(string storageKey, CancellationToken ct)
        {
            spy.LastDeleteToken = ct;

            // "Honours" the token -- mirrors what GcsFileStore.DeleteAsync's real underlying SDK
            // call does when handed an already-cancelled token (refuses the delete outright), unlike
            // LocalFileStore.DeleteAsync, which ignores its own `ct` parameter entirely and would
            // hide this bug from every test.
            ct.ThrowIfCancellationRequested();
            return inner.DeleteAsync(storageKey, ct);
        }

        public Task<FileStoreResult<CatEcommerce.Shared.Common.Helpers.Unit>> PutVariantAsync(
            string storageKey, CatEcommerce.Shared.Common.Constants.FileVariant variant, Stream content, CancellationToken ct) =>
            inner.PutVariantAsync(storageKey, variant, content, ct);

        public Task<bool> ExistsAsync(string storageKey, CancellationToken ct) => inner.ExistsAsync(storageKey, ct);
    }
}
