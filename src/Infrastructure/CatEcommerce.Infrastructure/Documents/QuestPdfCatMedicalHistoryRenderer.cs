using QuestPDF.Drawing;
using QuestPDF.Drawing.Exceptions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CatEcommerce.Infrastructure.Documents;

/// <summary>
/// US-CAT-10. Renders profile, vaccinations, parasite treatments and weight history -- the
/// records that exist today. Diagnoses, prescriptions and lab results are omitted with an
/// explicit printed note, not faked (HO-CAT-001 section 20.5: Visit, Prescription and lab
/// results are unbuilt).
///
/// QuestPDF licensing: the Community licence is CONFIRMED by the owner (2026-09-26) --
/// HO-CAT-001 §1 Amended, §20.6 closed.
///
/// SECURITY gate, LOW (2026-09-26): the default font (Lato) has no Bengali glyphs, so a Bengali
/// cat name, breed, note or vaccine/product name used to crash the whole document with a
/// QuestPDF DocumentDrawingException -- a real denial of US-CAT-10 for a Bangladesh clinic's
/// ordinary customers. <see cref="BengaliFallbackFontFamily"/> is registered once per process
/// (Noto Sans Bengali, SIL Open Font License 1.1, bundled under Documents/Fonts/) and named
/// second in every <c>FontFamily(...)</c> call in this file, so QuestPDF falls back to it for any
/// glyph Lato does not have.
/// </summary>
public sealed class QuestPdfCatMedicalHistoryRenderer : ICatMedicalHistoryPdfRenderer
{
    private const string BengaliFallbackFontFamily = "Noto Sans Bengali";
    private const string BengaliFontEmbeddedResourceName =
        "CatEcommerce.Infrastructure.Documents.Fonts.NotoSansBengali-Regular.ttf";

    private static readonly object FontRegistrationLock = new();
    private static bool _bengaliFontRegistered;

    public QuestPdfCatMedicalHistoryRenderer()
    {
        EnsureBengaliFallbackFontRegistered();
    }

    private static void EnsureBengaliFallbackFontRegistered()
    {
        if (_bengaliFontRegistered) return;

        lock (FontRegistrationLock)
        {
            if (_bengaliFontRegistered) return;

            using var fontStream = typeof(QuestPdfCatMedicalHistoryRenderer).Assembly
                .GetManifestResourceStream(BengaliFontEmbeddedResourceName);

            if (fontStream is null)
                throw new InvalidOperationException(
                    $"Embedded font resource '{BengaliFontEmbeddedResourceName}' was not found.");

            FontManager.RegisterFontFromStream(fontStream);
            _bengaliFontRegistered = true;
        }
    }

    /// <summary>
    /// ARCHITECTURE/CODE_REVIEW gates, LOW (2026-09-26, second pass): QuestPDF's own
    /// <see cref="DocumentDrawingException"/> (thrown for a glyph neither Lato nor the Bengali
    /// fallback font has) is caught here and translated into the port-level
    /// <see cref="CatMedicalHistoryRenderException"/>, so <c>CatsController</c> never has to
    /// reference a QuestPDF type.
    /// </summary>
    public byte[] Render(CatMedicalHistoryDocument document)
    {
        try
        {
            return RenderCore(document);
        }
        catch (DocumentDrawingException ex)
        {
            throw new CatMedicalHistoryRenderException(ex);
        }
    }

    private static byte[] RenderCore(CatMedicalHistoryDocument document)
    {
        var pdf = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Lato", BengaliFallbackFontFamily));

                page.Header().Column(column =>
                {
                    column.Item().Text("CatEcommerce Clinic -- Medical History").FontSize(16).Bold();
                    column.Item().Text($"{document.Name} ({document.Species}{(document.Breed is null ? "" : $", {document.Breed}")})")
                        .FontSize(13);
                });

                page.Content().Column(column =>
                {
                    column.Spacing(12);

                    column.Item().Element(c => ComposeProfile(c, document));
                    column.Item().Element(c => ComposeVaccinations(c, document));
                    column.Item().Element(c => ComposeParasiteTreatments(c, document));
                    column.Item().Element(c => ComposeWeightHistory(c, document));

                    column.Item().PaddingTop(10).Text(
                        "Note: diagnoses, prescriptions and lab results are not included in this " +
                        "report. Those clinical records (Visit, Prescription and Lab Result) are not " +
                        "yet part of this system.").Italic().FontSize(8);
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Generated ").FontSize(8);
                    text.Span(DateTimeOffset.UtcNow.ToString("u")).FontSize(8);
                });
            });
        });

        return pdf.GeneratePdf();
    }

    private static void ComposeProfile(QuestPDF.Infrastructure.IContainer container, CatMedicalHistoryDocument d)
    {
        container.Column(column =>
        {
            column.Item().Text("Profile").Bold().FontSize(12);
            column.Item().Text($"Sex: {d.Sex}");
            column.Item().Text($"Date of birth: {(d.DateOfBirth?.ToString() ?? "unknown")} ({d.DateOfBirthPrecision})");
            column.Item().Text($"Neuter status: {d.NeuterStatus}{(d.NeuteredOn is null ? "" : $" ({d.NeuteredOn})")}");
            if (d.IsHighRisk)
                column.Item().Text($"HIGH-RISK: {d.HandlingNote}").Bold();
        });
    }

    private static void ComposeVaccinations(QuestPDF.Infrastructure.IContainer container, CatMedicalHistoryDocument d)
    {
        container.Column(column =>
        {
            column.Item().Text("Vaccinations").Bold().FontSize(12);

            if (d.Vaccinations.Count == 0)
            {
                column.Item().Text("None recorded.");
                return;
            }

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(3);
                    c.RelativeColumn(2);
                    c.RelativeColumn(2);
                    c.RelativeColumn(2);
                });

                table.Header(header =>
                {
                    header.Cell().Text("Vaccine").Bold();
                    header.Cell().Text("Batch").Bold();
                    header.Cell().Text("Administered").Bold();
                    header.Cell().Text("Next due").Bold();
                });

                foreach (var v in d.Vaccinations)
                {
                    table.Cell().Text(v.VaccineName);
                    table.Cell().Text(v.BatchReference);
                    table.Cell().Text(v.AdministeredOn.ToString());
                    table.Cell().Text(v.NextDueOn.ToString());
                }
            });
        });
    }

    private static void ComposeParasiteTreatments(QuestPDF.Infrastructure.IContainer container, CatMedicalHistoryDocument d)
    {
        container.Column(column =>
        {
            column.Item().Text("Parasite Treatments").Bold().FontSize(12);

            if (d.ParasiteTreatments.Count == 0)
            {
                column.Item().Text("None recorded.");
                return;
            }

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(3);
                    c.RelativeColumn(3);
                    c.RelativeColumn(2);
                    c.RelativeColumn(2);
                });

                table.Header(header =>
                {
                    header.Cell().Text("Type").Bold();
                    header.Cell().Text("Product").Bold();
                    header.Cell().Text("Administered").Bold();
                    header.Cell().Text("Next due").Bold();
                });

                foreach (var t in d.ParasiteTreatments)
                {
                    table.Cell().Text(t.TreatmentType);
                    table.Cell().Text(t.ProductName);
                    table.Cell().Text(t.AdministeredOn.ToString());
                    table.Cell().Text(t.NextDueOn.ToString());
                }
            });
        });
    }

    private static void ComposeWeightHistory(QuestPDF.Infrastructure.IContainer container, CatMedicalHistoryDocument d)
    {
        container.Column(column =>
        {
            column.Item().Text("Weight History").Bold().FontSize(12);

            if (d.WeightHistory.Count == 0)
            {
                column.Item().Text("None recorded.");
                return;
            }

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(2);
                    c.RelativeColumn(2);
                });

                table.Header(header =>
                {
                    header.Cell().Text("Date").Bold();
                    header.Cell().Text("Weight (kg)").Bold();
                });

                foreach (var w in d.WeightHistory)
                {
                    table.Cell().Text(w.RecordedOn.ToString());
                    table.Cell().Text(w.Kilograms.ToString("0.00"));
                }
            });
        });
    }
}
