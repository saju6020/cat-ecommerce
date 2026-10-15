using CatEcommerce.Shared.Common.Constants;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>HO-RPT-001 section 8.13 -- a port over the static <see cref="ReportCatalogue"/> so tests can add definitions (a fixture report that sleeps, writes, or returns many rows).</summary>
public interface IReportCatalogue
{
    IReadOnlyList<ReportDefinition> All { get; }

    ReportDefinition? Find(string key);
}

/// <summary>The production catalogue: exactly the static one.</summary>
public sealed class StaticReportCatalogue : IReportCatalogue
{
    public IReadOnlyList<ReportDefinition> All => ReportCatalogue.All;

    public ReportDefinition? Find(string key) => ReportCatalogue.Find(key);
}
