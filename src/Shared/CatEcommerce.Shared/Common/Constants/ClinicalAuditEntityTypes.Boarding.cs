namespace CatEcommerce.Shared.Common.Constants;

/// <summary>Boarding's audit <c>EntityType</c> strings that touch clinical records (HO-XCT-001 section 5.2 C-ALL-5): the interceptor's <c>DataChange</c> rows are named after the
/// CLR aggregate root. The stay (care profile, arrival health observation), care tasks, observations, isolation entries and incidents describe care given to a named cat, so the
/// audit log redacts their values for a caller without <c>Cats.View</c>.</summary>
public static partial class ClinicalAuditEntityTypes
{
    private static readonly IReadOnlyList<string> BoardingEntries =
    [
        "BoardingStayAggregate", "CareTaskAggregate", "CareObservationAggregate", "IsolationEntryAggregate", "BoardingIncidentAggregate",
    ];
}
