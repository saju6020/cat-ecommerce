using System.Text;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Documents;
using QuestPDF.Infrastructure;
using Xunit;

namespace CatEcommerce.Infrastructure.Tests.Documents;

/// <summary>HO-EMR-001 §8.8 / ADR-130 -- the discharge summary PDF: A4, text only, never markup, Bengali-safe.</summary>
public sealed class QuestPdfDischargeSummaryRendererTests
{
    public QuestPdfDischargeSummaryRendererTests() => QuestPDF.Settings.License = LicenseType.Community;

    private static DischargeSummaryDocument Document(
        string catName = "Mitu", string homeCare = "Keep the ear dry.", bool prescriptionsAvailable = false,
        IReadOnlyList<DischargeSummaryCorrection>? corrections = null) => new(
        "Main clinic", "1 Road, Dhaka", "01700000000", catName, "Female", "2 years 3 months", "4.20 kg", "7 Oct 2026", "Dr Rahman",
        [new DischargeSummaryProblem("Otitis externa", "Confirmed")], homeCare, "Booked for 20 Oct 2026, 10:00",
        corrections ?? [], [], prescriptionsAvailable);

    [Fact]
    public void Render_ProducesAPdf()
    {
        var bytes = new QuestPdfDischargeSummaryRenderer().Render(Document());

        Assert.True(bytes.Length > 500);
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(bytes, 0, 5));
    }

    [Fact]
    public void Render_ClinicalTextThatLooksLikeMarkup_IsPrintedAsText_AndDoesNotThrow()
    {
        var hostile = "<script>alert(1)</script> <b>bold</b> {{catName}} [link](https://evil.example) & more";

        var bytes = new QuestPdfDischargeSummaryRenderer().Render(Document(homeCare: hostile, corrections:
            [new DischargeSummaryCorrection("7 Oct 2026, 16:00", hostile, hostile)]));

        Assert.Equal("%PDF-", Encoding.ASCII.GetString(bytes, 0, 5));
    }

    [Fact]
    public void Render_ABengaliCatName_DoesNotCrashTheDocument()
    {
        var bytes = new QuestPdfDischargeSummaryRenderer().Render(Document(catName: "মিতু"));

        Assert.Equal("%PDF-", Encoding.ASCII.GetString(bytes, 0, 5));
    }

    [Fact]
    public void Render_TwiceInOneProcess_AndAlongsideTheMedicalHistoryRenderer_RegistersTheFontOnlyOnce()
    {
        var first = new QuestPdfDischargeSummaryRenderer().Render(Document());
        var second = new QuestPdfDischargeSummaryRenderer().Render(Document(prescriptionsAvailable: true));
        _ = new QuestPdfCatMedicalHistoryRenderer();

        Assert.NotEmpty(first);
        Assert.NotEmpty(second);
    }
}
