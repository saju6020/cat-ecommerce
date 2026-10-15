using System.Reflection;
using CatEcommerce.Domain.DomainServices;
using Platform.Infrastructure.Core.Domain;
using Xunit;

namespace CatEcommerce.Application.Tests.CommandHandlers.Boarding;

/// <summary>
/// HO-BRD-001 section 15 group X (written by the TEST gate; the build shipped no Application-level Boarding test). These are structural guards: they fail the build rather than rely on review.
/// </summary>
public sealed class BoardingConventionTests
{
    private static readonly Assembly Application = typeof(CatEcommerce.Application.Commands.Boarding.ReserveBoardingStayCommand).Assembly;

    private static IEnumerable<Type> HandlerTypes(string ns) =>
        Application.GetTypes().Where(t => t.Namespace == ns && t is { IsClass: true, IsAbstract: false } && !t.Name.Contains('<'));

    private static IEnumerable<Type> CtorParameterTypes(Type t) =>
        t.GetConstructors(BindingFlags.Public | BindingFlags.Instance).SelectMany(c => c.GetParameters()).Select(p => p.ParameterType);

    // Handover 15 U-X4 names Billing, Inventory, Clinic (appointments live there) and Administration. The Cat and StoredFile aggregates are READ by the gate and the media handlers (HO-CAT-001 / HO-FIL-001 are
    // read contexts of Boarding), so they are deliberately not in the list.
    private static readonly HashSet<string?> Forbidden =
    [
        "CatEcommerce.Domain.Aggregates.Billing", "CatEcommerce.Domain.Aggregates.Inventory", "CatEcommerce.Domain.Aggregates.Clinic", "CatEcommerce.Domain.Aggregates.Administration",
    ];

    // U-X4 / ADR-232 -- a Boarding command handler writes only Boarding aggregates; other areas are reached through ports.
    [Fact]
    public void U_X4_NoBoardingCommandHandler_InjectsARepositoryOfAnotherAreasAggregate()
    {
        var handlers = HandlerTypes("CatEcommerce.Application.CommandHandlers.Boarding")
            .Where(t => t.GetInterfaces().Any(i => i.IsGenericType && i.Name.StartsWith("ICommandHandlerAsync", StringComparison.Ordinal))).ToList();
        Assert.True(handlers.Count > 30, "expected the Boarding command handlers to be found; found " + handlers.Count);

        var offenders = handlers
            .SelectMany(h => CtorParameterTypes(h).Where(p => p.IsGenericType && p.GetGenericTypeDefinition() == typeof(IAggregateRootRepository<>))
                .Select(p => p.GetGenericArguments()[0]).Where(a => Forbidden.Contains(a.Namespace))
                .Select(a => $"{h.Name} -> IAggregateRootRepository<{a.Name}>"))
            .ToList();

        Assert.True(offenders.Count == 0, string.Join("; ", offenders.Distinct()));
    }

    // ADR-365 (ratified by the coordinator, narrowly): Boarding command handlers hold no Billing repository; they record charges only through IBillableChargeRecorder and request invoices only
    // through IssueInvoiceFromChargesCommand. This allow-list fails the build if another Billing seam is injected into a Boarding command handler.
    private static readonly HashSet<string> AllowedBillingSeams = [nameof(IBillableChargeRecorder)];

    [Fact]
    public void U_X4b_TheOnlyBillingSeamABoardingCommandHandlerInjects_IsTheChargeRecorder()
    {
        var handlers = HandlerTypes("CatEcommerce.Application.CommandHandlers.Boarding")
            .Where(t => t.GetInterfaces().Any(i => i.IsGenericType && i.Name.StartsWith("ICommandHandlerAsync", StringComparison.Ordinal))).ToList();

        static IEnumerable<Type> Flatten(Type t) => t.IsGenericType ? new[] { t }.Concat(t.GetGenericArguments().SelectMany(Flatten)) : [t];

        var offenders = handlers
            .SelectMany(h => CtorParameterTypes(h).SelectMany(Flatten)
                .Where(t => !t.Name.StartsWith("IBoarding", StringComparison.Ordinal) && !t.Name.StartsWith("Boarding", StringComparison.Ordinal))
                .Where(t => (t.Namespace ?? "").Contains("Billing", StringComparison.Ordinal) || t.Name.Contains("Billable", StringComparison.Ordinal) || t.Name.Contains("Invoice", StringComparison.Ordinal))
                .Where(t => !AllowedBillingSeams.Contains(t.Name))
                .Select(t => $"{h.Name} -> {t.Name}"))
            .ToList();

        Assert.True(offenders.Count == 0, string.Join("; ", offenders.Distinct()));
    }

    // U-X5 / HO-NTF-001 ADR-057 -- a domain-event handler never enqueues a notification; the command handler does, before its save.
    [Fact]
    public void U_X5_NoBoardingDomainEventHandler_InjectsTheNotificationOutbox()
    {
        var handlers = HandlerTypes("CatEcommerce.Application.DomainEventHandlers.Boarding").ToList();
        Assert.True(handlers.Count >= 5, "expected the Boarding event handlers to be found; found " + handlers.Count);

        var offenders = handlers.Where(h => CtorParameterTypes(h).Any(p => p == typeof(INotificationOutbox))).Select(h => h.Name).ToList();

        Assert.True(offenders.Count == 0, string.Join("; ", offenders));
    }
}
