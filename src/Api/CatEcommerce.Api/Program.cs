// CatEcommerce.Api — THE single host.
//
// Composition is governed by `.ai/host-composition.md`. Read it before editing this file.
// CommandWorker and EventWorker no longer exist (ADR-010): this host runs command handlers,
// query handlers and domain event handlers in-process.
//
// Controllers inject IInMemoryDispatcher (Platform.Infrastructure.Core), NOT IDispatcher.
// IInMemoryDispatcher has no bus methods at all, so a controller physically cannot reach a
// broker by accident (ADR-019).
//
// Do NOT hand-register anything AddInMemoryBusServices() provides:
//   IInMemoryDispatcher, IResolver, IHandlerResolver, ICommandSender, IQueryProcessor,
//   IValidationService, IValidationProvider
//
// Do NOT add AddServiceBusProvider(...) — it routes commands to a RabbitMQ broker that does not
// exist (ADR-020). Do NOT reference IDispatcher, IEventPublisher or IBusMessageDispatcher
// anywhere in this solution.
//
// Per-area registrations live in Modules/<Area>Module.cs, one file per functional area, each
// adding exactly one `Add<Area>Module(this IServiceCollection, IConfiguration)` call below
// (ADR-341). This file should never need another edit when a new area is implemented — only a
// new module file plus its one registration line here.

using CatEcommerce.Api.Hosting;
using CatEcommerce.Api.Modules;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Auditing;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseDefaultServiceProvider((_, options) =>
{
    // Keep BOTH on in every environment — this is what catches a missing registration at
    // startup instead of at first dispatch (ADR-018, .ai/di-registration-guide.md section 4).
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});

// ConcurrencyExceptionFilter maps DbUpdateConcurrencyException (ADR-016, xmin) to 409. It is an
// MVC exception filter, not middleware: it runs inside MVC's own filter pipeline, closer to the
// controller than any middleware-level handler, and does not rethrow -- so it converts the
// exception to 409 before SafeExceptionHandlerMiddleware (below) would ever see it. See
// Filters/ConcurrencyExceptionFilter.cs.
//
// UniqueConstraintExceptionFilter (HO-ACC-001 §12.4, extended by HO-ADM-001 §12.5) and
// NulByteRejectionFilter (HO-ADM-001, SEC LOW hardening round) used to be registered as global
// MvcOptions filters from AccountsModule and AdministrationModule respectively. Integrator move,
// 2026-09-26 (HO-ADM-001-ACCEPTANCE integrator note, HO-ADM-001-ARCHITECTURE MEDIUM 1): both are
// host-wide, not area-specific -- UniqueConstraintExceptionFilter now maps constraint violations
// for several areas, and NulByteRejectionFilter always applies to every controller in the host --
// so both are registered here, once, alongside ConcurrencyExceptionFilter, the same way every
// other host-wide MVC filter is. Order is unchanged from before the move (each still runs as an
// ordinary, unordered MvcOptions.Filters entry) and each is registered exactly once.
builder.Services.AddControllers(options =>
{
    options.Filters.Add<CatEcommerce.Api.Filters.ConcurrencyExceptionFilter>();
    options.Filters.Add<CatEcommerce.Api.Filters.UniqueConstraintExceptionFilter>();
    options.Filters.Add<CatEcommerce.Api.Filters.NulByteRejectionFilter>();
});

// MapHealthChecks("/health") is called explicitly in the pipeline below (this host no longer
// calls the platform's UseHttpPipeline(), which used to map it internally -- SECURITY gate,
// dropping UseHttpPipeline() entirely). Not itself a dispatch-pipeline service, so registering it
// does not violate the "never hand-register" list in .ai/host-composition.md section 5.
builder.Services.AddHealthChecks().AddCheck<CatEcommerce.Api.Hosting.DeliveryTaxClassHealthCheck>("delivery-tax-class");

// ── Platform: the in-memory dispatch pipeline ────────────────────────────────
// Registers exactly IResolver, IHandlerResolver, IInMemoryDispatcher, ICommandSender,
// IQueryProcessor, IValidationService, IValidationProvider — all Scoped, and nothing else
// (Core 1.0.34). No IEventPublisher, no broker type.
builder.Services.AddInMemoryBusServices();

// ── REQUIRED: the one seam the platform declares but does not fill (ADR-018) ────
// CommandSender needs IUserContextProvider and no implementation ships. Per §12.9,
// AccountsModule.AddAccountsModule registers IUserContextProvider -> SessionUserContextProvider
// (replacing what used to be a DevUserContextProvider line here) -- registering it a second time
// in this file would leave two competing IUserContextProvider registrations resolving side by
// side (ARCHITECTURE gate, MEDIUM): harmless only by accident of container build order, and a
// latent trap for any future IEnumerable<IUserContextProvider> resolution. Do not re-add a line
// here; Accounts owns this registration for as long as it is the only area with a real identity
// source.

// ── REMOVED: AddServiceBusProvider(builder.Configuration) ─────────────────────
// That call would route commands to RabbitMQ. Without it, AddInMemoryBusServices() dispatches
// in-process, which is the entire point of ADR-013. Do not re-add it.

// ── Persistence: EF Core over PostgreSQL ─────────────────────────────────────
// The two audit interceptors (§11.9, §12.1, ADR-040) are wired directly in this options
// delegate -- not via a second CatEcommerceDbContext constructor -- so they cannot fail open:
// every CatEcommerceDbContext this application resolves through DI is built by this one
// delegate (ARCHITECTURE gate on HO-ACC-001; see CatEcommerceDbContext.Accounts.cs).
builder.Services.AddDbContext<CatEcommerceDbContext>((sp, o) =>
{
    o.UseNpgsql(builder.Configuration.GetConnectionString("CatEcommerce"));
    o.AddInterceptors(
        // HO-XCT-001 ADR-299 layer 2. The two dedicated immutability guards keep running first: they raise their own, more specific errors for the
        // tables they cover (audit entries, the controlled-drug register) and have no side effects. The retention guard runs next and BEFORE the
        // change-capture interceptor, so a refused delete is never audited as though it happened (it aborts the save).
        new AuditEntryImmutabilityInterceptor(),
        // HO-PHM-001 §8.9 layer 2 (ADR-144): a modified or deleted controlled-drug register entry or seal is refused before SQL is sent.
        new ControlledDrugRegisterImmutabilityInterceptor(),
        new CatEcommerce.Infrastructure.Retention.HardDeleteGuardInterceptor(),
        new AuditChangeCaptureInterceptor(
            sp.GetRequiredService<IClientIpAccessor>(),
            sp.GetRequiredService<IUserContextProvider>(),
            sp.GetRequiredService<TimeProvider>()));
});

// Our EF Core implementation of the platform's IAggregateRootRepository<T> contract
// (.ai/persistence-ef-core.md section 2). Registered once, open-generic.
builder.Services.AddScoped(typeof(IAggregateRootRepository<>), typeof(EfAggregateRootRepository<>));

// ── Host-wide pipeline concerns (ADR-342, owner decision B, 2026-09-25) ──────────
// Forwarded-headers trust and the CORS allow-list apply ahead of every area, so they are
// registered here, once, as shared host code -- not inside any area's own module (ADR-341).
builder.Services.AddHostPipelineServices(builder.Configuration);

// ── Per-area modules (ADR-341) ────────────────────────────────────────────────
// One call per functional area, each in its own Modules/<Area>Module.cs. Areas not yet built
// are empty stubs so this file never needs another edit when one is implemented — only its
// module file changes, plus the one line adding it below the first time it is built.
builder.Services
    .AddAccountsModule(builder.Configuration)
    .AddCatProfilesModule(builder.Configuration)
    .AddAdministrationModule(builder.Configuration)
    .AddNotificationsModule(builder.Configuration)
    .AddAppointmentsModule(builder.Configuration)
    .AddFileStorageModule(builder.Configuration)
    .AddShopModule(builder.Configuration)
    .AddInventoryModule(builder.Configuration)
    .AddConsultationModule(builder.Configuration)
    .AddPharmacyModule(builder.Configuration)
    .AddSurgeryModule(builder.Configuration)
    .AddBillingModule(builder.Configuration)
    .AddOrdersModule(builder.Configuration)
    .AddBoardingModule(builder.Configuration)
    .AddSubscriptionsModule(builder.Configuration)
    .AddPortalModule(builder.Configuration)
    .AddReportingModule(builder.Configuration)
    .AddCrossCuttingModule(builder.Configuration);

var app = builder.Build();

// ── Shared pipeline prefix (ADR-342, rewritten -- SECURITY gate + ARCHITECTURE third pass) ─────
// One host-level block, run exactly once, ahead of every area's own middleware:
//   UseForwardedHeaders() -> UseRouting() -> SafeExceptionHandlerMiddleware -> UseCors() -> [per-area] -> MapHealthChecks() -> MapControllers()
//
// This host no longer calls `UseHttpPipeline()` (Platform.Infrastructure.Host.WebApi) at all. Two
// SECURITY findings on the platform's own composition forced that:
//   1. Its CORS policy reflects any origin (SetIsOriginAllowed(_ => true)) together with
//      AllowCredentials() -- exploitable once this handover adds cookie sessions, since
//      SameSite=Lax still lets same-site origins send them.
//   2. Its GlobalExceptionHandlerMiddleware reads and logs the full request body for a POST with
//      Content-Type: application/json. Placing SafeExceptionHandlerMiddleware ahead of it (the
//      first version of this fix) only stopped the leak for exceptions thrown in the session
//      middleware and the rate limiter -- the platform handler still sat nested around
//      UseCors/UseEndpoints, so it still caught (and could still log the body for) anything MVC
//      itself throws before model binding (a resource/authorization filter, controller
//      activation). Dropping `UseHttpPipeline()` entirely removes that inner instance, so
//      SafeExceptionHandlerMiddleware -- registered once, right here -- is the ONLY exception
//      handler anywhere in this pipeline, and it never reads the body, a header or the query
//      string, for ANY exception from ANY position.
//
// What UseHttpPipeline(enableAuthorization: false) actually did (decompiled,
// Platform.Infrastructure.Host.WebApi 1.0.14), and what replaces each piece:
//   - ServiceIdHeaderMiddleware / TenantIdHeaderMiddleware: both gated on flags this call never
//     passed as true. Never active. Dropped; nothing replaces them.
//   - UseRouting(): now called explicitly, once, earlier in this same block.
//   - GlobalExceptionHandlerMiddleware: replaced by SafeExceptionHandlerMiddleware (Api/Hosting/),
//     which never reads the body, a header or the query string.
//   - UseCors(policy => SetIsOriginAllowed(_ => true).AllowCredentials()...): replaced by
//     UseCors(HostPipelineExtensions.CorsPolicyName), an explicit origin allow-list from
//     configuration (Cors:AllowedOrigins), empty and therefore fail-closed by default. Registered
//     as shared host code (AddHostPipelineServices above), not owned by any area (owner decision
//     B, HO-ACC-001 §1 Amended (18), 2026-09-25).
//   - UseAuthentication()/UseAuthorization(): gated on `enableAuthorization`, which this call
//     passed as false. Never active; this solution has no ASP.NET Core Identity scheme to
//     authenticate against (ADR-024). Not replaced -- authorization is `[RequireAction]` (ADR-036).
//   - UseEndpoints(endpoints => { MapControllerRoute("Default", "{controller}/{action}/{id?}");
//     MapHealthChecks("/health"); }): MapHealthChecks("/health") is kept, called explicitly below.
//     MapControllerRoute (conventional routing) is dropped -- every controller in this solution
//     uses attribute routing (`[Route("api/...")]`); no action ever depended on the conventional
//     template, and grepping confirms no controller lacks a `[Route]` attribute.
app.UseForwardedHeaders();
app.UseRouting();
app.UseMiddleware<CatEcommerce.Api.Hosting.SafeExceptionHandlerMiddleware>();
app.UseCors(CatEcommerce.Api.Hosting.HostPipelineExtensions.CorsPolicyName);

app.UseAccountsModule();

// Gate fix 2026-09-27 (carried from HO-ADM-001-ARCHITECTURE MEDIUM 1, integrator-authorised):
// UseRateLimiter() moved out of AccountsModule.UseAccountsModule() now that Notifications is a
// second area contributing its own rate-limit policies -- it is host composition, not something
// either area owns. Must run AFTER UseAccountsModule() (session/caller resolution): the
// customer/IP-partitioned ProfileUpdate policy reads ResolvedCaller from HttpContext.Items, which
// SessionAuthenticationMiddleware (inside UseAccountsModule()) is what writes.
app.UseRateLimiter();

app.MapHealthChecks("/health");
app.MapControllers();

app.Run();

// Needed for WebApplicationFactory<Program> in integration tests.
public partial class Program;
