using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Infrastructure.Reporting;

/// <summary>The clinic name printed on the PDF header. Who generated the file is on the result itself.</summary>
public sealed record ReportRenderContext(string ClinicName);

/// <summary>
/// HO-RPT-001 section 8.12 / 8.13 (ADR-285) -- the CSV and PDF shapes of one <see cref="ReportResult"/>. The same code renders an interactive export and a scheduled one, so a scheduled
/// attachment is byte-for-byte what the recipient could export on screen (I-G2). Stateless, so registered as a singleton.
/// </summary>
public interface IReportExportRenderer
{
    ReportFormat Format { get; }

    string ContentType { get; }

    string Extension { get; }

    byte[] Render(ReportResult result, ReportRenderContext context);
}
