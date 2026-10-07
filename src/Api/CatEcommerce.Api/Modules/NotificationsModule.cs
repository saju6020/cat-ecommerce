// Notifications module -- HO-NTF-001.
//
// Registers every command handler, validator, query handler, domain event handler and domain
// service this area needs, plus the Notification worker (the second asynchronous element in the
// solution, alongside nothing else -- ADR-060/ADR-065) and its own rate-limit policies. Program.cs
// already calls AddNotificationsModule(builder.Configuration) (added ahead of this handover, per
// ADR-341) -- this file is the only one that needed to change.

using CatEcommerce.Api.Background;
using CatEcommerce.Api.RateLimiting;
using CatEcommerce.Application.CommandHandlers.Notification;
using CatEcommerce.Application.CommandHandlers.Validators;
using CatEcommerce.Application.Commands.Notification;
using CatEcommerce.Application.DomainEventHandlers.Notification;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Events.Account;
using CatEcommerce.Infrastructure.Configuration;
using CatEcommerce.Infrastructure.Notifications;
using CatEcommerce.Infrastructure.Notifications.Guards;
using CatEcommerce.Infrastructure.Retention.Erasers;
using CatEcommerce.Read.Queries.Notification;
using CatEcommerce.Read.QueryHandlers.Notification;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using FluentValidation;
using Microsoft.AspNetCore.RateLimiting;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Events;
using Platform.Infrastructure.Core.Queries;
using Platform.Infrastructure.Core.Validation;
using RateLimitPartition = System.Threading.RateLimiting.RateLimitPartition;

namespace CatEcommerce.Api.Modules;

public static class NotificationsModule
{
    public static IServiceCollection AddNotificationsModule(this IServiceCollection services, IConfiguration configuration)
    {
        // ── Options (§12.2) ─────────────────────────────────────────────────────────────────────
        services.AddOptions<NotificationOptions>()
            .Bind(configuration.GetSection(NotificationOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<Microsoft.Extensions.Options.IValidateOptions<NotificationOptions>, NotificationOptionsValidator>();

        // ── §8.13 ports (Scoped, except the catalogue and the sender resolver -- Singleton) ─────
        services.AddScoped<INotificationOutbox, EfNotificationOutbox>();
        services.AddScoped<INotificationRequestStore, EfNotificationRequestStore>();
        services.AddScoped<INotificationClaimStore, EfNotificationClaimStore>();
        services.AddScoped<INotificationEnlister, EfNotificationEnlister>();
        services.AddScoped<INotificationResendCounter, EfNotificationResendCounter>();
        services.AddScoped<INotificationResendStore, EfNotificationResendStore>();
        services.AddScoped<INotificationRecipientDirectory, AccountRecipientDirectory>();
        services.AddScoped<INotificationLinkBuilder, NotificationLinkBuilder>();
        services.AddScoped<IQuietHoursCalendar, QuietHoursCalendar>();
        services.AddScoped<INotificationDeliverySettings, NotificationDeliverySettings>();
        services.AddScoped<INotificationSubjectGuard, CatVaccinationDueGuard>();
        services.AddScoped<INotificationSubjectGuard, CatDewormingDueGuard>();
        services.AddScoped<ISubjectGuardResolver, SubjectGuardResolver>();

        services.AddSingleton<INotificationTemplateCatalogue>(new StaticNotificationTemplateCatalogue());
        services.AddSingleton<NotificationSenderResolver>();
        services.AddSingleton<INotificationChannelSenderResolver>(sp => sp.GetRequiredService<NotificationSenderResolver>());

        // HO-NTF-001 §11.4 (deviation from §13's file list, recorded in HO-NTF-001 §1 Amended /
        // HO-ACC-001 §1 Amended): the ONLY IPasswordResetTokenDispatcher registration in the
        // solution. AccountsModule used to register its own Development/Undelivered dispatchers
        // here (removed 2026-09-26 gate fix -- corrected 2026-09-27, this comment previously said
        // "registered a second time ... wins", which stopped being true the moment those two
        // classes were deleted; there has never been a second registration to win against).
        services.AddScoped<IPasswordResetTokenDispatcher, NotificationPasswordResetTokenDispatcher>();

        services.AddScoped<IPersonalDataEraser, NotificationPersonalDataEraser>();

        RegisterCommands(services);
        RegisterQueries(services);
        RegisterDomainEventHandlers(services);

        // ── Worker (§12.2, ADR-060/065) ──────────────────────────────────────────────────────────
        services.AddScoped<NotificationWorkQueue>();
        services.AddScoped<CareReminderSweep>();
        services.AddScoped<NotificationWorkCycle>();
        services.AddHostedService<NotificationWorker>();

        // ── Rate limiting (§12.4/§12.7) -- a second AddRateLimiter call composes with Accounts'.
        // Reads the permit limits from NotificationOptions (not a literal), so a test factory can
        // raise/lower them the same way AccountsApiFactory does for its own policies. ─────────────
        services.AddRateLimiter(options =>
        {
            options.AddPolicy(NotificationRateLimitPolicies.Unsubscribe, context =>
            {
                var limits = context.RequestServices.GetRequiredService<Microsoft.Extensions.Options.IOptions<NotificationOptions>>().Value.Unsubscribe;
                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                    {
                        PermitLimit = limits.PermitLimit, Window = TimeSpan.FromMinutes(limits.WindowMinutes), QueueLimit = 0
                    });
            });

            options.AddPolicy(NotificationRateLimitPolicies.TemplatePreview, context =>
            {
                var limits = context.RequestServices.GetRequiredService<Microsoft.Extensions.Options.IOptions<NotificationOptions>>().Value.TemplatePreview;
                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                    {
                        PermitLimit = limits.PermitLimit, Window = TimeSpan.FromMinutes(limits.WindowMinutes), QueueLimit = 0
                    });
            });
        });

        return services;
    }

    private static void RegisterCommands(IServiceCollection services)
    {
        services.AddScoped<ICommandHandlerAsync<SaveNotificationTemplateCommand>, SaveNotificationTemplateCommandHandler>();
        services.AddScoped<IValidator<SaveNotificationTemplateCommand>, SaveNotificationTemplateCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<ResetNotificationTemplateCommand>, ResetNotificationTemplateCommandHandler>();
        services.AddScoped<IValidator<ResetNotificationTemplateCommand>, ResetNotificationTemplateCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<RequestNotificationCommand>, RequestNotificationCommandHandler>();
        services.AddScoped<IValidator<RequestNotificationCommand>, RequestNotificationCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<ExpandNotificationRequestCommand>, ExpandNotificationRequestCommandHandler>();
        services.AddScoped<IValidator<ExpandNotificationRequestCommand>, ExpandNotificationRequestCommandValidator>();

        // Gate fix 2026-09-27 (SECURITY/CODE_REVIEW second pass, MEDIUM): dispatched by
        // NotificationWorkCycle, from a fresh scope, when ExpandNotificationRequestCommand's own
        // dispatch throws.
        services.AddScoped<ICommandHandlerAsync<RecordNotificationExpansionFailureCommand>, RecordNotificationExpansionFailureCommandHandler>();
        services.AddScoped<IValidator<RecordNotificationExpansionFailureCommand>, RecordNotificationExpansionFailureCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<DeliverNotificationCommand>, DeliverNotificationCommandHandler>();
        services.AddScoped<IValidator<DeliverNotificationCommand>, DeliverNotificationCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<RedactNotificationSecretsCommand>, RedactNotificationSecretsCommandHandler>();
        services.AddScoped<IValidator<RedactNotificationSecretsCommand>, RedactNotificationSecretsCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<ResendNotificationCommand>, ResendNotificationCommandHandler>();
        services.AddScoped<IValidator<ResendNotificationCommand>, ResendNotificationCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<MarkInboxItemReadCommand>, MarkInboxItemReadCommandHandler>();
        services.AddScoped<IValidator<MarkInboxItemReadCommand>, MarkInboxItemReadCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<UnsubscribeFromMarketingCommand>, UnsubscribeFromMarketingCommandHandler>();
        services.AddScoped<IValidator<UnsubscribeFromMarketingCommand>, UnsubscribeFromMarketingCommandValidator>();
    }

    private static void RegisterQueries(IServiceCollection services)
    {
        services.AddScoped<IQueryHandlerAsync<GetNotificationTemplatesQuery, IReadOnlyList<NotificationTemplateSummaryViewModel>>, GetNotificationTemplatesQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetNotificationTemplateQuery, NotificationTemplateDetailViewModel?>, GetNotificationTemplateQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<PreviewNotificationTemplateQuery, NotificationTemplatePreviewViewModel>, PreviewNotificationTemplateQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetNotificationLogQuery, PagedResult<NotificationLogItemViewModel>>, GetNotificationLogQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetNotificationDetailQuery, NotificationDetailViewModel?>, GetNotificationDetailQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetNotificationSummaryQuery, NotificationSummaryViewModel>, GetNotificationSummaryQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetInboxQuery, PagedResult<InboxItemViewModel>>, GetInboxQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetInboxUnreadCountQuery, int>, GetInboxUnreadCountQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetDueCareRemindersQuery, IReadOnlyList<DueCareReminderRow>>, GetDueCareRemindersQueryHandler>();
    }

    private static void RegisterDomainEventHandlers(IServiceCollection services)
    {
        services.AddScoped<IEventHandlerAsync<CustomerMergedEvent>, ReassignInboxOnCustomerMergedHandler>();
        services.AddScoped<IEventHandlerAsync<CustomerAccountAnonymisedEvent>, AnonymiseNotificationsOnCustomerAnonymisedHandler>();
    }
}
