using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;
using CatEcommerce.Shared.Common.Constants;

namespace CatEcommerce.Infrastructure.Reporting;

/// <summary>
/// HO-RPT-001 section 10.2 -- configuration, section "Reporting" (no secrets). The CSV and PDF row caps default to <see cref="ReportRules"/> and are lowered only by the tests that test them
/// (section 16.1 rule 4).
/// </summary>
public sealed class ReportingOptions
{
    public const string SectionName = "Reporting";

    /// <summary>Statement timeout of one report or tile query (ADR-279). 5-120 seconds.</summary>
    [Range(5, 120)]
    public int QueryTimeoutSeconds { get; set; } = 30;

    /// <summary>Reports in flight at once (ADR-290).</summary>
    [Range(1, 64)]
    public int MaxConcurrentReports { get; set; } = 4;

    /// <summary>Reports (and dashboard calls) one staff account may have in flight at once, so one user cannot fill the host-wide limit.</summary>
    [Range(1, 16)]
    public int MaxConcurrentPerAccount { get; set; } = 2;

    [Range(0, 256)]
    public int ConcurrencyQueueLimit { get; set; } = 8;

    [Range(1, 1_000_000)]
    public int CsvMaxRows { get; set; } = ReportRules.CsvMaxRows;

    [Range(1, 1_000_000)]
    public int PdfMaxRows { get; set; } = ReportRules.PdfMaxRows;

    [ValidateObjectMembers]
    public ReportingWorkerOptions Worker { get; set; } = new();
}

public sealed class ReportingWorkerOptions
{
    public bool Enabled { get; set; } = true;

    [Range(5, 3600)]
    public int IntervalSeconds { get; set; } = 60;
}
