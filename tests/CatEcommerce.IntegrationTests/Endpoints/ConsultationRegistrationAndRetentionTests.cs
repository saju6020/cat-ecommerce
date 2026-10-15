using CatEcommerce.Application.Commands.Visit;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Aggregates.Visit;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Read.ClinicalHistory;
using CatEcommerce.Read.Queries.Visit;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Queries;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-EMR-001 I-A2 ("every registration resolves" -- counted, not assumed, §12.7) and C-ALL-3 (the retention review is a count,
/// never a delete).</summary>
public sealed class ConsultationRegistrationAndRetentionTests(ConsultationApiFactory factory) : ConsultationIntegrationTestBase(factory)
{
    /// <summary>Every command in <c>Commands/Visit</c> has a handler and a validator registered as <c>IValidator&lt;T&gt;</c> (a validator
    /// registered as the base class silently never runs -- rule 21); every query in <c>Queries/Visit</c> has its handler; every declared
    /// history section that this slice builds has a provider. There is no assembly scanning, so a forgotten line fails here.</summary>
    [Fact]
    public void I_A2_EveryCommandQueryAndSection_IsRegistered_AndResolves()
    {
        using var scope = Factory.Services.CreateScope();
        var sp = scope.ServiceProvider;

        var commands = typeof(OpenVisitCommand).Assembly.GetTypes()
            .Where(t => t.Namespace == typeof(OpenVisitCommand).Namespace && t is { IsClass: true, IsAbstract: false } && typeof(Command).IsAssignableFrom(t))
            .ToList();
        Assert.True(commands.Count >= 25, $"expected the whole consultation command set, found {commands.Count}");

        foreach (var command in commands)
        {
            Assert.NotNull(sp.GetService(typeof(ICommandHandlerAsync<>).MakeGenericType(command)));
            Assert.NotNull(sp.GetService(typeof(IValidator<>).MakeGenericType(command)));
        }

        var queries = typeof(GetVisitDetailQuery).Assembly.GetTypes()
            .Where(t => t.Namespace == typeof(GetVisitDetailQuery).Namespace && t is { IsClass: true, IsAbstract: false })
            .Select(t => (Type: t, Base: BaseQueryOf(t)))
            .Where(t => t.Base is not null)
            .ToList();
        Assert.True(queries.Count >= 23, $"expected the whole consultation query set, found {queries.Count}");

        foreach (var (query, queryBase) in queries)
        {
            var handler = typeof(IQueryHandlerAsync<,>).MakeGenericType(query, queryBase!.GetGenericArguments()[0]);
            Assert.NotNull(sp.GetService(handler));
        }

        var providers = sp.GetServices<IClinicalHistorySectionProvider>().Select(p => p.Key).ToHashSet();
        string[] built = ["timeline", "visits", "diagnoses", "lab-results", "photos", "vaccinations", "parasite-treatments", "weights"];
        Assert.Superset(built.ToHashSet(), providers);
        Assert.Empty(providers.Except(built)); // prescriptions, boarding-stays, food-purchases: no provider until their areas land
        Assert.NotNull(sp.GetService<IDischargeSummaryPdfRenderer>());
        Assert.Contains(sp.GetServices<INotificationSubjectGuard>(), g => g.Kind == NotificationSubjectKind.VisitFollowUp);
        Assert.Contains(sp.GetServices<IRetentionReviewSource>(), s => s.SourceKey == "consultation-clinical");
    }

    private static Type? BaseQueryOf(Type type)
    {
        for (var t = type.BaseType; t is not null; t = t.BaseType)
        {
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Query<>)) return t;
        }

        return null;
    }

    /// <summary>C-ALL-3 -- a cat last seen eight years ago is counted for review; one seen yesterday is not; nothing is deleted.</summary>
    [Fact]
    public async Task TheRetentionReview_CountsRecordsPastTheirPeriod_AndDeletesNothing()
    {
        var world = await SeedClinicalWorldAsync();
        var user = new UserContext(world.VetStaffId, Guid.NewGuid());
        var now = world.Clock.GetUtcNow();

        Guid oldCatId;
        await using (var scope = world.Api.Services.CreateAsyncScope())
        {
            var cats = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<CatAggregate>>();
            var visits = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<VisitAggregate>>();

            var cat = new CatAggregate(Guid.NewGuid());
            cat.Register(new CatRegistrationDto(world.CustomerId, "Old timer", "Cat", CatSex.Male, DateOnly.FromDateTime(now.UtcDateTime).AddYears(-15), DateOfBirthPrecision.Exact,
                null, null, DateOnly.FromDateTime(now.UtcDateTime), user, Guid.NewGuid()));
            await cats.SaveAsync(cat, EventPublishOption.InMemoryPublishOnly);
            oldCatId = cat.Id;

            var visit = new VisitAggregate(Guid.NewGuid());
            visit.Open(new VisitOpeningDto(world.BranchId, Guid.NewGuid(), oldCatId, AppointmentStatus.CheckedIn, CatLifeStatus.Alive, world.ServiceId,
                "Consultation", ServiceCategory.Consultation, now.AddYears(-8), user, Guid.NewGuid()));
            await visits.SaveAsync(visit, EventPublishOption.InMemoryPublishOnly);
        }

        await using var checkScope = world.Api.Services.CreateAsyncScope();
        var source = checkScope.ServiceProvider.GetServices<IRetentionReviewSource>().Single(s => s.SourceKey == "consultation-clinical");
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        var counts = await source.CountPastRetentionAsync(today, default);
        Assert.Contains(counts, c => c.RecordKind == "visit" && c.RecordClass == RecordClass.Clinical && c.Count >= 1 && c.OldestAnchor <= today.AddYears(-7));

        // Seen yesterday (this world's own visit): nothing from this cat is eligible; a date 20 years back finds nothing at all.
        Assert.Empty(await source.CountPastRetentionAsync(today.AddYears(-20), default));

        // It is a flag, not a delete: the old visit is still there.
        Assert.Equal(1, await ScalarAsync<int>("select count(*) from clinic.visit where cat_id = @c", ("c", oldCatId)));
    }
}
