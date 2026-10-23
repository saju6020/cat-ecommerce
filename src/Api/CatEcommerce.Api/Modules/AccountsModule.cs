// Accounts module -- HO-ACC-001.
//
// Registers everything Accounts and Identity needs: command handlers + validators, query
// handlers, domain event handlers, security ports and their implementations, CAPTCHA, rate
// limiting and the session/caller-resolution seam. Moved out of Program.cs by ADR-341 so this
// area never needs to touch a file another area also edits.
//
// The two audit interceptors are wired in Program.cs's own `AddDbContext` options delegate, not
// here and not via a second CatEcommerceDbContext constructor -- see
// CatEcommerceDbContext.Accounts.cs and Program.cs (ARCHITECTURE gate on HO-ACC-001, §12.1).
//
// ADR-342 (rewritten; owner-ratified drop of UseHttpPipeline(), HO-ACC-001 §1 Amended (18),
// 2026-09-25 -- see .ai/decisions/adr.md): Program.cs owns ONE shared pipeline prefix --
// `UseForwardedHeaders()` -> `UseRouting()` -> the host's own `SafeExceptionHandlerMiddleware`
// (Api/Hosting/) -> `UseCors(...)` -- run exactly once, before any area's own middleware.
// `UseAccountsModule()` below therefore adds only Accounts' own two middleware (session
// resolution, rate limiting), not the shared prefix. Forwarded-headers configuration and the CORS
// policy itself are host-wide concerns, not an Accounts-owned policy, so both are registered from
// `Api/Hosting/HostPipelineExtensions.cs` and called once from Program.cs's own shared block, not
// from this file (owner decision B).

using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Filters;
using CatEcommerce.Api.RateLimiting;
using CatEcommerce.Api.Sessions;
using CatEcommerce.Application.CommandHandlers.Account;
using CatEcommerce.Application.CommandHandlers.Validators;
using CatEcommerce.Application.Commands.Account;
using CatEcommerce.Application.DomainEventHandlers.Account;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Events.Account;
using CatEcommerce.Domain.Events.Cat;
using CatEcommerce.Infrastructure.Configuration;
using CatEcommerce.Infrastructure.Hosting;
using CatEcommerce.Infrastructure.Notifications;
using CatEcommerce.Infrastructure.Security;
using CatEcommerce.Infrastructure.Security.Captcha;
using CatEcommerce.Read.Queries.Account;
using CatEcommerce.Read.QueryHandlers.Account;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using FluentValidation;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Events;
using Platform.Infrastructure.Core.Queries;
using Platform.Infrastructure.Core.Validation;
using RateLimitPartition = System.Threading.RateLimiting.RateLimitPartition;

namespace CatEcommerce.Api.Modules;

public static class AccountsModule
{
    public static IServiceCollection AddAccountsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHostedService<AccountStartupGuardHostedService>();

        // ── Options (§12.2) ─────────────────────────────────────────────────────────────────────
        services.AddOptions<AccountOptions>()
            .Bind(configuration.GetSection(AccountOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<CaptchaOptions>()
            .Bind(configuration.GetSection(CaptchaOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.Configure<RateLimitingOptions>(configuration.GetSection(RateLimitingOptions.SectionName));

        // ── Security ports -> implementations (§8.10, §12.2) ───────────────────────────────────
        services.AddScoped<IPasswordHasher, Argon2idPasswordHasher>();
        services.AddScoped<IBreachedPasswordChecker, LocalListBreachedPasswordChecker>();
        services.AddSingleton<ISessionTokenIssuer, OpaqueSessionTokenIssuer>();
        services.AddSingleton<IPasswordResetTokenGenerator, RandomPasswordResetTokenGenerator>();
        services.AddScoped<IClientIpAccessor, HttpContextClientIpAccessor>();
        services.AddScoped<ISessionPolicy, AccountOptionsSessionPolicy>();
        services.TryAddSingleton(TimeProvider.System);

        // HO-NTF-001 §11.4/§12.7 (deviation from §13's file list, recorded in HO-ACC-001 §1 Amended
        // and HO-NTF-001 §1 Amended): the two Development/Undelivered dispatchers this module used
        // to register here are DELETED -- replaced by NotificationsModule's own
        // NotificationPasswordResetTokenDispatcher, which is now the ONLY IPasswordResetTokenDispatcher
        // registration in the solution (corrected 2026-09-27: this comment used to say "registered a
        // second time ... wins", which stopped being true once these two classes were deleted). No
        // registration is left here on purpose: Accounts no longer owns how a reset token is
        // delivered.

        // ── CAPTCHA (§12.6, ADR-029) -- Singleton, owns its own MemoryCache ─────────────────────
        services.AddSingleton<ICaptchaImageRenderer, SkiaCaptchaImageRenderer>();
        services.AddSingleton<ICaptchaService, MemoryCacheCaptchaService>();
        services.AddScoped<CaptchaValidationFilter>();

        // ── Caller resolution (§12.5, ADR-032) -- replaces DevUserContextProvider ───────────────
        // Program.cs no longer registers IUserContextProvider at all (§12.9: this registration
        // replaces that line, not competes with it) -- this is the only IUserContextProvider
        // registration in the solution. DevUserContextProvider is kept registered as its own
        // concrete type for SessionUserContextProvider's Development fallback.
        services.AddHttpContextAccessor();
        services.AddScoped<DevUserContextProvider>();
        services.AddScoped<Platform.Infrastructure.Common.Security.IUserContextProvider, SessionUserContextProvider>();

        // ── Command handlers (C1-C18) + validators (as IValidator<T>, F3) ──────────────────────
        services.AddScoped<ICommandHandlerAsync<RegisterCustomerCommand>, RegisterCustomerCommandHandler>();
        services.AddScoped<IValidator<RegisterCustomerCommand>, RegisterCustomerCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<SignInCustomerCommand>, SignInCustomerCommandHandler>();
        services.AddScoped<IValidator<SignInCustomerCommand>, SignInCustomerCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<SignOutCustomerCommand>, SignOutCustomerCommandHandler>();
        services.AddScoped<IValidator<SignOutCustomerCommand>, SignOutCustomerCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<RequestPasswordResetCommand>, RequestPasswordResetCommandHandler>();
        services.AddScoped<IValidator<RequestPasswordResetCommand>, RequestPasswordResetCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<ResetPasswordCommand>, ResetPasswordCommandHandler>();
        services.AddScoped<IValidator<ResetPasswordCommand>, ResetPasswordCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<UpdateCustomerContactDetailsCommand>, UpdateCustomerContactDetailsCommandHandler>();
        services.AddScoped<IValidator<UpdateCustomerContactDetailsCommand>, UpdateCustomerContactDetailsCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<AddDeliveryAddressCommand>, AddDeliveryAddressCommandHandler>();
        services.AddScoped<IValidator<AddDeliveryAddressCommand>, AddDeliveryAddressCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<UpdateDeliveryAddressCommand>, UpdateDeliveryAddressCommandHandler>();
        services.AddScoped<IValidator<UpdateDeliveryAddressCommand>, UpdateDeliveryAddressCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<RemoveDeliveryAddressCommand>, RemoveDeliveryAddressCommandHandler>();
        services.AddScoped<IValidator<RemoveDeliveryAddressCommand>, RemoveDeliveryAddressCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<SetDefaultDeliveryAddressCommand>, SetDefaultDeliveryAddressCommandHandler>();
        services.AddScoped<IValidator<SetDefaultDeliveryAddressCommand>, SetDefaultDeliveryAddressCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<ChangeNotificationPreferencesCommand>, ChangeNotificationPreferencesCommandHandler>();
        services.AddScoped<IValidator<ChangeNotificationPreferencesCommand>, ChangeNotificationPreferencesCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<CreateStaffAccountCommand>, CreateStaffAccountCommandHandler>();
        services.AddScoped<IValidator<CreateStaffAccountCommand>, CreateStaffAccountCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<ChangeStaffRolesCommand>, ChangeStaffRolesCommandHandler>();
        services.AddScoped<ICommandHandlerAsync<SetStaffPasswordCommand>, SetStaffPasswordCommandHandler>();
        services.AddScoped<IValidator<SetStaffPasswordCommand>, SetStaffPasswordCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<UpdateStaffAccountDetailsCommand>, UpdateStaffAccountDetailsCommandHandler>();
        services.AddScoped<IValidator<UpdateStaffAccountDetailsCommand>, UpdateStaffAccountDetailsCommandValidator>();
        services.AddScoped<IValidator<ChangeStaffRolesCommand>, ChangeStaffRolesCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<RecordAuthorizationDenialCommand>, RecordAuthorizationDenialCommandHandler>();
        services.AddScoped<IValidator<RecordAuthorizationDenialCommand>, RecordAuthorizationDenialCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<SignInStaffCommand>, SignInStaffCommandHandler>();
        services.AddScoped<IValidator<SignInStaffCommand>, SignInStaffCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<SignOutStaffCommand>, SignOutStaffCommandHandler>();
        services.AddScoped<IValidator<SignOutStaffCommand>, SignOutStaffCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<RecordStaffSessionActivityCommand>, RecordStaffSessionActivityCommandHandler>();
        services.AddScoped<IValidator<RecordStaffSessionActivityCommand>, RecordStaffSessionActivityCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<RefreshCustomerSessionCsrfTokenCommand>, RefreshCustomerSessionCsrfTokenCommandHandler>();
        services.AddScoped<IValidator<RefreshCustomerSessionCsrfTokenCommand>, RefreshCustomerSessionCsrfTokenCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<RefreshStaffSessionCsrfTokenCommand>, RefreshStaffSessionCsrfTokenCommandHandler>();
        services.AddScoped<IValidator<RefreshStaffSessionCsrfTokenCommand>, RefreshStaffSessionCsrfTokenCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<RegisterWalkInCustomerCommand>, RegisterWalkInCustomerCommandHandler>();
        services.AddScoped<IValidator<RegisterWalkInCustomerCommand>, RegisterWalkInCustomerCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<MergeCustomerCommand>, MergeCustomerCommandHandler>();
        services.AddScoped<IValidator<MergeCustomerCommand>, MergeCustomerCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<RecordPersonalDataExportCommand>, RecordPersonalDataExportCommandHandler>();
        services.AddScoped<IValidator<RecordPersonalDataExportCommand>, RecordPersonalDataExportCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<DeleteCustomerAccountCommand>, DeleteCustomerAccountCommandHandler>();
        services.AddScoped<IValidator<DeleteCustomerAccountCommand>, DeleteCustomerAccountCommandValidator>();

        // ── Query handlers (Q-C1 .. Q-J1, §12.3) ────────────────────────────────────────────────
        services.AddScoped<IQueryHandlerAsync<GetCurrentCustomerSessionQuery, CustomerSessionViewModel?>, GetCurrentCustomerSessionQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetCustomerProfileQuery, CustomerProfileViewModel?>, GetCustomerProfileQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetDeliveryAddressesQuery, DeliveryAddressBookViewModel>, GetDeliveryAddressesQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetNotificationPreferencesQuery, NotificationPreferencesViewModel>, GetNotificationPreferencesQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetStaffRoleCatalogueQuery, StaffRoleCatalogueViewModel>, GetStaffRoleCatalogueQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetStaffAccountsQuery, PagedResult<StaffAccountSummaryViewModel>>, GetStaffAccountsQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetStaffAccountQuery, StaffAccountViewModel?>, GetStaffAccountQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetStaffSessionStatusQuery, StaffSessionStatusViewModel?>, GetStaffSessionStatusQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetStaffCustomerQuery, StaffCustomerViewModel?>, GetStaffCustomerQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<FindCustomersQuery, IReadOnlyList<StaffCustomerSummaryViewModel>>, FindCustomersQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetDuplicateCustomerGroupsQuery, PagedResult<DuplicateCustomerGroupViewModel>>, GetDuplicateCustomerGroupsQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetPersonalDataExportQuery, PersonalDataExportViewModel?>, GetPersonalDataExportQueryHandler>();

        // ── Domain event handlers (§11.6/§11.7) ─────────────────────────────────────────────────
        services.AddScoped<IEventHandlerAsync<CustomerSignedInEvent>, AuditCustomerSignedInHandler>();
        services.AddScoped<IEventHandlerAsync<StaffSignedInEvent>, AuditStaffSignedInHandler>();
        services.AddScoped<IEventHandlerAsync<CustomerSignInFailedEvent>, AuditCustomerSignInFailedHandler>();
        services.AddScoped<IEventHandlerAsync<StaffSignInFailedEvent>, AuditStaffSignInFailedHandler>();
        services.AddScoped<IEventHandlerAsync<CustomerSessionIssuedEvent>, AuditCustomerSessionIssuedHandler>();
        services.AddScoped<IEventHandlerAsync<StaffSessionIssuedEvent>, AuditStaffSessionIssuedHandler>();
        services.AddScoped<IEventHandlerAsync<CustomerSessionEndedEvent>, AuditCustomerSessionEndedHandler>();
        services.AddScoped<IEventHandlerAsync<StaffSessionEndedEvent>, AuditStaffSessionEndedHandler>();
        services.AddScoped<IEventHandlerAsync<StaffSessionIdleExpiredEvent>, AuditStaffSessionIdleExpiredHandler>();
        services.AddScoped<IEventHandlerAsync<PasswordResetRequestedEvent>, AuditPasswordResetRequestedHandler>();
        services.AddScoped<IEventHandlerAsync<CustomerPasswordSetEvent>, AuditCustomerPasswordSetHandler>();
        services.AddScoped<IEventHandlerAsync<StaffPasswordSetEvent>, AuditStaffPasswordSetHandler>();
        services.AddScoped<IEventHandlerAsync<StaffAccountDetailsChangedEvent>, AuditStaffAccountDetailsChangedHandler>();
        services.AddScoped<IEventHandlerAsync<CustomerMergedEvent>, AuditCustomerMergedHandler>();
        services.AddScoped<IEventHandlerAsync<CustomerMergedEvent>, ReassignCatsOnCustomerMergedHandler>();
        services.AddScoped<IEventHandlerAsync<CatOwnerReassignedOnMergeEvent>, AuditCatOwnerReassignedOnMergeHandler>();
        services.AddScoped<IEventHandlerAsync<CustomerAccountAnonymisedEvent>, AuditCustomerAnonymisedHandler>();
        services.AddScoped<IEventHandlerAsync<CustomerAccountAnonymisedEvent>, ClearAddressBookOnAnonymisedHandler>();
        services.AddScoped<IEventHandlerAsync<CustomerAccountAnonymisedEvent>, ClearPreferencesOnAnonymisedHandler>();
        services.AddScoped<IEventHandlerAsync<CustomerPersonalDataExportedEvent>, AuditCustomerPersonalDataExportedHandler>();

        // ── MVC filter (§12.4) -- UniqueConstraintExceptionFilter used to be registered here,
        // alongside ConcurrencyExceptionFilter, never widening it. Integrator move, 2026-09-26
        // (HO-ADM-001-ACCEPTANCE integrator note, HO-ADM-001-ARCHITECTURE MEDIUM 1): it is now
        // registered from Program.cs's own AddControllers(...) call, alongside
        // ConcurrencyExceptionFilter and NulByteRejectionFilter, since HO-ADM-001 extended it to map
        // its own areas' unique-constraint violations too -- it is no longer Accounts-only. See
        // Program.cs.

        // ── Rate limiting (§12.7, ADR-030) -- this area's own policies only. The 429 response shape
        // (OnRejected) is the shared host base in HostPipelineExtensions.AddHostPipelineServices,
        // called once from Program.cs ahead of every area's module (gate fix 2026-09-27, carried
        // from HO-ADM-001-ARCHITECTURE MEDIUM 1, integrator-authorised: Notifications is now a
        // second area contributing its own policies, so the shared base moved out of this
        // area-owned module). This call still composes correctly with that one and with
        // Notifications' own -- ASP.NET Core's AddRateLimiter adds to the same RateLimiterOptions
        // instance regardless of how many times it is called. ────────────────────────────────────
        services.AddRateLimiter(options =>
        {
            var rateLimiting = configuration.GetSection(RateLimitingOptions.SectionName).Get<RateLimitingOptions>() ?? new RateLimitingOptions();

            AddFixedWindowPolicy(options, RateLimitPolicies.CaptchaIssue, rateLimiting.CaptchaIssue);
            AddFixedWindowPolicy(options, RateLimitPolicies.Registration, rateLimiting.Registration);
            AddFixedWindowPolicy(options, RateLimitPolicies.SignIn, rateLimiting.SignIn);
            AddFixedWindowPolicy(options, RateLimitPolicies.PasswordReset, rateLimiting.PasswordReset);
            AddFixedWindowPolicy(options, RateLimitPolicies.AccountSensitive, rateLimiting.AccountSensitive);
            AddCustomerOrIpPartitionedPolicy(options, RateLimitPolicies.ProfileUpdate, rateLimiting.ProfileUpdate);
        });

        // ── Sessions ─────────────────────────────────────────────────────────────────────────────
        // Forwarded headers and CORS used to be registered here too, but both are host-wide
        // pipeline concerns applied ahead of every area, not an Accounts-owned policy -- they now
        // live in Api/Hosting/HostPipelineExtensions.cs, registered once from Program.cs's shared
        // block (ARCHITECTURE gate, fourth pass, MEDIUM; owner decision B, HO-ACC-001 §1
        // Amended (18), 2026-09-25).
        services.AddScoped<SessionCookieWriter>();

        return services;
    }

    /// <summary>
    /// Applies Accounts' own one pipeline middleware: session/caller resolution. Forwarded headers,
    /// routing, the shared exception boundary, CORS and now the rate limiter itself (gate fix
    /// 2026-09-27, carried from HO-ADM-001-ARCHITECTURE MEDIUM 1) are Program.cs's job now (ADR-342,
    /// rewritten) -- calling them again here would stack a second copy in front of every area
    /// registered after this one, which is exactly the "N modules, N handlers" pattern the rewrite
    /// removes. <c>UseRateLimiter()</c> must still run AFTER this middleware in the pipeline
    /// (Program.cs calls it immediately following <c>UseAccountsModule()</c>): the
    /// customer/IP-partitioned policy below reads <c>ResolvedCaller</c> from
    /// <c>HttpContext.Items</c>, which <see cref="SessionAuthenticationMiddleware"/> is what
    /// writes. See Program.cs's shared pipeline block.
    /// </summary>
    public static IApplicationBuilder UseAccountsModule(this IApplicationBuilder app)
    {
        app.UseMiddleware<SessionAuthenticationMiddleware>();
        return app;
    }

    private static void AddFixedWindowPolicy(RateLimiterOptions options, string name, RateLimitPolicyOptions policy) =>
        options.AddPolicy(name, context => RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = policy.PermitLimit,
                Window = TimeSpan.FromMinutes(policy.WindowMinutes),
                QueueLimit = 0
            }));

    /// <summary>
    /// HO-ACC-001 §1 Amended (18), follow-up (2026-09-25): the profile-update/email step-up path
    /// must not share a bucket with account deletion/export (§12.7's <c>AccountSensitive</c>, IP
    /// partitioned), and customers behind shared NAT or clinic Wi-Fi must not block each other.
    /// Partitioned by the signed-in customer id when one is resolved -- read from
    /// <c>HttpContext.Items[ResolvedCaller.HttpContextItemsKey]</c>, written by
    /// <c>SessionAuthenticationMiddleware</c>, which runs before <c>UseRateLimiter()</c>
    /// (<c>UseAccountsModule</c>) -- falling back to IP only when no session resolved (this
    /// endpoint requires <c>[RequireCustomerSession]</c>, so an IP fallback here is only ever hit
    /// for an already-401 request; the policy still applies at that point since the rate limiter
    /// runs ahead of the controller's own auth check).
    /// </summary>
    private static void AddCustomerOrIpPartitionedPolicy(RateLimiterOptions options, string name, RateLimitPolicyOptions policy) =>
        options.AddPolicy(name, context =>
        {
            var caller = context.Items[ResolvedCaller.HttpContextItemsKey] as ResolvedCaller;
            var partitionKey = caller is { Kind: ResolvedCallerKind.Customer }
                ? $"customer:{caller.AccountId}"
                : $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";

            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: partitionKey,
                factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                {
                    PermitLimit = policy.PermitLimit,
                    Window = TimeSpan.FromMinutes(policy.WindowMinutes),
                    QueueLimit = 0
                });
        });
}

/// <summary>
/// §20.9 / §12.9 / I-A2. Runs at real host startup (an <see cref="IHostedService"/> throwing from
/// <see cref="StartAsync"/> aborts host startup -- for both <c>dotnet run</c> and
/// <c>WebApplicationFactory</c>-driven integration tests). Resolves <see cref="IHostEnvironment"/>
/// via DI rather than reading a raw "ASPNETCORE_ENVIRONMENT" configuration key, which does not
/// reflect <c>IWebHostBuilder.UseEnvironment(...)</c> as used by <c>WebApplicationFactory</c>.
/// </summary>
internal sealed class AccountStartupGuardHostedService(IHostEnvironment environment, IConfiguration configuration)
    : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var isDevelopmentOrTesting = environment.IsDevelopment() || environment.EnvironmentName == "Testing";

        if (!isDevelopmentOrTesting)
        {
            if (!string.IsNullOrEmpty(configuration["Captcha:TestAnswer"]))
                throw new InvalidOperationException("Captcha:TestAnswer must never be set outside Development/Testing (§20.9).");

            if (bool.TryParse(configuration["DevUser:Enabled"], out var devUserEnabled) && devUserEnabled)
                throw new InvalidOperationException("DevUser:Enabled must never be true outside Development (ADR-032).");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
