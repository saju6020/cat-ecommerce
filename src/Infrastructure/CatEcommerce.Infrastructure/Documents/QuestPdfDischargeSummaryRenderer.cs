using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using QuestPDF.Drawing;
using QuestPDF.Drawing.Exceptions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CatEcommerce.Infrastructure.Documents;

/// <summary>
/// HO-EMR-001 §8.8 / ADR-130 -- renders the derived discharge summary: A4, text only, branch letterhead. Every string,
/// clinical free text included, goes through <c>Text(...)</c> as plain text -- nothing is parsed as markup, so a note
/// that contains markup prints as characters (HO-EMR-001 §4.4: no HTML stored or rendered from clinical text).
/// QuestPDF's Community licence is the owner-confirmed one (HO-CAT-001 §20.6). The Bengali fallback font is registered
/// once per process, like the medical-history renderer, so a Bengali cat name does not crash the document.
/// Until HO-PHM-001 registers a contributor the summary prints that medicines to take home are on the prescription label.
/// </summary>
public sealed class QuestPdfDischargeSummaryRenderer : IDischargeSummaryPdfRenderer
{
    private const string BengaliFallbackFontFamily = "Noto Sans Bengali";
    private const string BengaliFontEmbeddedResourceName = "CatEcommerce.Infrastructure.Documents.Fonts.NotoSansBengali-Regular.ttf";

    private static readonly object FontRegistrationLock = new();
    private static bool _bengaliFontRegistered;

    public QuestPdfDischargeSummaryRenderer() => EnsureBengaliFallbackFontRegistered();

    private static void EnsureBengaliFallbackFontRegistered()
    {
        if (_bengaliFontRegistered) return;

        lock (FontRegistrationLock)
        {
            if (_bengaliFontRegistered) return;

            using var fontStream = typeof(QuestPdfDischargeSummaryRenderer).Assembly.GetManifestResourceStream(BengaliFontEmbeddedResourceName)
                ?? throw new InvalidOperationException($"Embedded font resource '{BengaliFontEmbeddedResourceName}' was not found.");

            FontManager.RegisterFontFromStream(fontStream);
            _bengaliFontRegistered = true;
        }
    }

    public byte[] Render(DischargeSummaryDocument document)
    {
        try
        {
            return Document.Create(container => container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Lato", BengaliFallbackFontFamily));

                page.Header().Column(column =>
                {
                    column.Item().Text(document.BranchName).FontSize(16).Bold();
                    column.Item().Text(document.BranchAddress);
                    if (!string.IsNullOrWhiteSpace(document.BranchPhone)) column.Item().Text(document.BranchPhone);
                    column.Item().PaddingTop(6).Text("Visit summary").FontSize(13).Bold();
                });

                page.Content().Column(column =>
                {
                    column.Spacing(10);

                    column.Item().Column(c =>
                    {
                        c.Item().Text($"Cat: {document.CatName} ({document.CatSex}, {document.CatAge})");
                        c.Item().Text($"Date of visit: {document.VisitDateLocal}");
                        c.Item().Text($"Seen by: {document.AttendingVet}");
                        if (document.WeightAtVisit is not null) c.Item().Text($"Weight at visit: {document.WeightAtVisit}");
                    });

                    Section(column, "Diagnosis", document.Problems.Select(p => $"{p.Name} ({p.Certainty})").ToList(), "No diagnosis recorded.");
                    Section(column, "Home care", document.DischargeInstructions.Length == 0 ? [] : [document.DischargeInstructions], "No home-care instructions were written.");

                    foreach (var extra in document.ExtraSections)
                        Section(column, extra.Title, extra.Lines, "None.");

                    if (!document.PrescriptionsAvailable)
                        column.Item().Text(CatEcommerce.Shared.Common.Constants.ClinicalMessages.PrescriptionsOnLabel).Italic();

                    if (document.FollowUp is not null)
                        Section(column, "Follow-up", [document.FollowUp], "");

                    if (document.Corrections.Count > 0)
                        Section(column, "Corrections", document.Corrections
                            .Select(c => $"{c.AddedOnLocal}: {c.Text}{(string.IsNullOrWhiteSpace(c.Reason) ? "" : $" (reason: {c.Reason})")}").ToList(), "");
                });

                page.Footer().AlignCenter().Text("This summary was prepared from the clinic's record of the visit.").FontSize(8);
            })).GeneratePdf();
        }
        catch (DocumentDrawingException ex)
        {
            // The message of QuestPDF's exception can quote a glyph from the text: keep it out of ours.
            throw new InvalidOperationException("The visit summary could not be rendered.", ex);
        }
    }

    private static void Section(ColumnDescriptor column, string title, IReadOnlyList<string> lines, string emptyText)
    {
        column.Item().Column(c =>
        {
            c.Item().Text(title).Bold().FontSize(12);
            if (lines.Count == 0)
            {
                if (emptyText.Length > 0) c.Item().Text(emptyText);
                return;
            }

            foreach (var line in lines) c.Item().Text(line);
        });
    }
}
