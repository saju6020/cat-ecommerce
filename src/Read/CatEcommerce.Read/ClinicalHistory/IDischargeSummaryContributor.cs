using CatEcommerce.Read.ViewModels;

namespace CatEcommerce.Read.ClinicalHistory;

/// <summary>
/// HO-EMR-001 §8.8 / §4.2 -- the extension point of the discharge summary. HO-PHM-001 registers a contributor to append
/// the prescriptions issued at the visit; until one is registered the summary carries <c>prescriptionsAvailable: false</c>
/// and the printed summary says that medicines to take home are on the prescription label. A contributor only reads.
/// </summary>
public interface IDischargeSummaryContributor
{
    /// <summary>A section for the finalised visit, or <c>null</c> when the contributor has nothing to add.</summary>
    Task<DischargeSummarySectionViewModel?> ContributeAsync(Guid visitId, Guid catId, CancellationToken ct);
}
