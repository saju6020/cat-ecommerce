// CatProfiles (CAT) module -- HO-CAT-001.
//
// Registers everything Cat Profiles needs: command handlers + validators, query handlers,
// domain event handlers and the two document services (QR code, PDF medical history). Moved out
// of Program.cs by ADR-341 so this area never needs to touch a file another area also edits.

using CatEcommerce.Application.CommandHandlers.Cat;
using CatEcommerce.Application.CommandHandlers.Validators;
using CatEcommerce.Application.CommandHandlers.Vaccination;
using CatEcommerce.Application.Commands.Cat;
using CatEcommerce.Application.Commands.Vaccination;
using CatEcommerce.Application.DomainEventHandlers.Cat;
using CatEcommerce.Domain.Events.Cat;
using CatEcommerce.Infrastructure.Configuration;
using CatEcommerce.Infrastructure.Documents;
using CatEcommerce.Read.Queries.Cat;
using CatEcommerce.Read.Queries.Vaccination;
using CatEcommerce.Read.QueryHandlers.Cat;
using CatEcommerce.Read.QueryHandlers.Vaccination;
using CatEcommerce.Read.ViewModels;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Events;
using Platform.Infrastructure.Core.Queries;
using QuestPDF.Infrastructure;

namespace CatEcommerce.Api.Modules;

public static class CatProfilesModule
{
    public static IServiceCollection AddCatProfilesModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CatProfileOptions>(configuration.GetSection(CatProfileOptions.SectionName));

        // Owner decision, 2026-09-27 (HO-CAT-001 §1 Amended, ACCEPTANCE gate A22): the ONE shared
        // public web address, registered here because Cat Profiles (the cage-card QR) is its first
        // consumer -- Notifications switches to it once both areas have merged (integrator task,
        // not touched by this handover). See PublicSiteOptions.cs for the full rationale.
        services.Configure<PublicSiteOptions>(configuration.GetSection(PublicSiteOptions.SectionName));
        services.AddHostedService<PublicSiteStartupGuardHostedService>();

        // ── Command handlers (all 13, C1-C13) + validators (all 13, as IValidator<T>) ──────────
        services.AddScoped<ICommandHandlerAsync<RegisterCatCommand>, RegisterCatCommandHandler>();
        services.AddScoped<IValidator<RegisterCatCommand>, RegisterCatCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<UpdateCatProfileCommand>, UpdateCatProfileCommandHandler>();
        services.AddScoped<IValidator<UpdateCatProfileCommand>, UpdateCatProfileCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<RecordCatNeuterStatusCommand>, RecordCatNeuterStatusCommandHandler>();
        services.AddScoped<IValidator<RecordCatNeuterStatusCommand>, RecordCatNeuterStatusCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<RecordCatWeightCommand>, RecordCatWeightCommandHandler>();
        services.AddScoped<IValidator<RecordCatWeightCommand>, RecordCatWeightCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<UpdateCatMedicalProfileCommand>, UpdateCatMedicalProfileCommandHandler>();
        services.AddScoped<IValidator<UpdateCatMedicalProfileCommand>, UpdateCatMedicalProfileCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<AddCatPhotoCommand>, AddCatPhotoCommandHandler>();
        services.AddScoped<IValidator<AddCatPhotoCommand>, AddCatPhotoCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<FlagCatHighRiskCommand>, FlagCatHighRiskCommandHandler>();
        services.AddScoped<IValidator<FlagCatHighRiskCommand>, FlagCatHighRiskCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<ClearCatHighRiskFlagCommand>, ClearCatHighRiskFlagCommandHandler>();
        services.AddScoped<IValidator<ClearCatHighRiskFlagCommand>, ClearCatHighRiskFlagCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<RecordCatVaccinationCommand>, RecordCatVaccinationCommandHandler>();
        services.AddScoped<IValidator<RecordCatVaccinationCommand>, RecordCatVaccinationCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<RecordCatParasiteTreatmentCommand>, RecordCatParasiteTreatmentCommandHandler>();
        services.AddScoped<IValidator<RecordCatParasiteTreatmentCommand>, RecordCatParasiteTreatmentCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<MarkCatDeceasedCommand>, MarkCatDeceasedCommandHandler>();
        services.AddScoped<IValidator<MarkCatDeceasedCommand>, MarkCatDeceasedCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<TransferCatOwnershipCommand>, TransferCatOwnershipCommandHandler>();
        services.AddScoped<IValidator<TransferCatOwnershipCommand>, TransferCatOwnershipCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<RegisterVaccineCommand>, RegisterVaccineCommandHandler>();
        services.AddScoped<IValidator<RegisterVaccineCommand>, RegisterVaccineCommandValidator>();

        // ── Query handlers (all 7, Q1-Q7) ───────────────────────────────────────────────────────
        services.AddScoped<IQueryHandlerAsync<GetCatProfileQuery, CatProfileViewModel?>, GetCatProfileQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetCatsQuery, PagedResult<CatListItemViewModel>>, GetCatsQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetCatWeightHistoryQuery, PagedResult<CatWeightPointViewModel>>, GetCatWeightHistoryQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetCatVaccinationStatusQuery, CatVaccinationStatusViewModel?>, GetCatVaccinationStatusQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetCatMedicalHistoryQuery, CatMedicalHistoryViewModel?>, GetCatMedicalHistoryQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetVaccinesQuery, PagedResult<VaccineListItemViewModel>>, GetVaccinesQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetCatCageCardQuery, CatCageCardViewModel?>, GetCatCageCardQueryHandler>();

        // ── Domain event handlers (all 3 audit handlers -- section 11.7) ───────────────────────
        services.AddScoped<IEventHandlerAsync<CatFlaggedHighRiskEvent>, AuditCatHighRiskFlaggedHandler>();
        services.AddScoped<IEventHandlerAsync<CatHighRiskFlagClearedEvent>, AuditCatHighRiskFlagClearedHandler>();
        services.AddScoped<IEventHandlerAsync<CatOwnershipTransferredEvent>, AuditCatOwnershipTransferredHandler>();

        // ── Documents (section 12.2). QuestPDF Community licence CONFIRMED by the owner
        // 2026-09-26 -- HO-CAT-001 §1 Amended, §20.6 closed. ─────────────────────────────────────
        QuestPDF.Settings.License = LicenseType.Community;
        services.AddScoped<ICatMedicalHistoryPdfRenderer, QuestPdfCatMedicalHistoryRenderer>();
        services.AddScoped<ICatQrCodeGenerator, QrCoderCatQrCodeGenerator>();

        return services;
    }
}

/// <summary>
/// Owner decision, 2026-09-27 (HO-CAT-001 §1 Amended, ACCEPTANCE gate A22). Validates
/// <see cref="PublicSiteOptions.WebBaseUrl"/> at real host startup -- the same
/// <see cref="IHostedService"/> pattern <c>AccountStartupGuardHostedService</c>
/// (<c>AccountsModule.cs</c>) already uses for an environment-conditional check a plain
/// DataAnnotation cannot express: resolves <see cref="IHostEnvironment"/> via DI rather than a raw
/// "ASPNETCORE_ENVIRONMENT" configuration key, which does not reflect
/// <c>IWebHostBuilder.UseEnvironment(...)</c> as used by <c>WebApplicationFactory</c> in tests.
/// </summary>
internal sealed class PublicSiteStartupGuardHostedService(
    IHostEnvironment environment,
    IOptions<PublicSiteOptions> options,
    ILogger<PublicSiteStartupGuardHostedService> logger)
    : IHostedService
{
    // Obvious "fill me in" markers left in a copied settings file. Deliberately narrow: hosts such
    // as example.com are legitimate values here (tests and demos use them).
    private static readonly string[] PlaceholderMarkers =
        ["<", ">", "{", "}", "changeme", "change-me", "todo", "yourdomain", "your-domain", "your_domain"];

    internal static bool LooksLikePlaceholder(string value)
        => PlaceholderMarkers.Any(marker => value.Contains(marker, StringComparison.OrdinalIgnoreCase));

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var webBaseUrl = options.Value.WebBaseUrl;
        var isTesting = environment.EnvironmentName == "Testing";

        // NTF backlog #7: an empty or placeholder base URL makes every reset/care/unsubscribe link
        // built from it relative and broken. Production refuses to start; every other environment
        // keeps starting (Development and Testing legitimately run without one) but says so.
        var isEmpty = string.IsNullOrWhiteSpace(webBaseUrl);
        if (isEmpty || LooksLikePlaceholder(webBaseUrl))
        {
            var problem = isEmpty ? "is empty" : "looks like a placeholder";
            if (environment.IsProduction())
                throw new InvalidOperationException(isEmpty
                    ? "PublicSite:WebBaseUrl is required in Production."
                    : "PublicSite:WebBaseUrl looks like a placeholder; set the real public web address in Production.");

            logger.LogWarning(
                "PublicSite:WebBaseUrl {Problem} in the {Environment} environment: links in notifications "
                + "(password reset, care reminders, unsubscribe) will be relative and will not work. "
                + "Set it to the site's public https address.",
                problem, environment.EnvironmentName);
            return Task.CompletedTask;
        }

        if (!Uri.TryCreate(webBaseUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new InvalidOperationException("PublicSite:WebBaseUrl must be an absolute http(s) URL.");

        if (uri.AbsolutePath != "/" || uri.Query.Length > 0 || uri.Fragment.Length > 0)
            throw new InvalidOperationException("PublicSite:WebBaseUrl must not include a path, query or fragment.");

        // CODE_REVIEW gate, MEDIUM (2026-09-27, fifth pass): Uri.TryCreate happily parses
        // "https://evil.example@trusted.example" as host "trusted.example" with UserInfo
        // "evil.example" -- a browser (and most humans) read the userinfo-prefixed string as the
        // domain, so a QR built from this value would be phishing-shaped even though the URI class
        // considers it well-formed. Reject any non-empty userinfo alongside path/query/fragment.
        if (uri.UserInfo.Length > 0)
            throw new InvalidOperationException("PublicSite:WebBaseUrl must not include userinfo (a \"user@\" prefix).");

        if (!environment.IsDevelopment() && !isTesting && uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("PublicSite:WebBaseUrl must use https outside Development/Testing.");

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
