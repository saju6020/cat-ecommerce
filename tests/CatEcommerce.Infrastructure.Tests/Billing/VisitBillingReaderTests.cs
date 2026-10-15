using CatEcommerce.Infrastructure.Consultation;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Infrastructure.Tests.Billing;

/// <summary>HO-BIL-001 Phase J -- the words a diagnostic test prints on the owner's invoice, and the stable keys of a visit's lines.</summary>
public sealed class VisitBillingReaderTests
{
    [Theory]
    [InlineData("BloodWork", null, "Blood work")]
    [InlineData("Urinalysis", "ignored for a named kind", "Urinalysis")]
    [InlineData("FelvFivTest", null, "FeLV/FIV test")]
    [InlineData("Imaging", "Abdominal ultrasound", "Imaging: Abdominal ultrasound")]
    [InlineData("Other", "  Skin scrape  ", "Diagnostic test: Skin scrape")]
    [InlineData("Other", null, "Diagnostic test")]
    [InlineData("Imaging", "   ", "Imaging")]
    public void ADiagnosticLine_PrintsItsKind_AndTheStudyOnlyWhereTheKindIsOpenEnded(string kindName, string? description, string expected)
    {
        var kind = new[] { DiagnosticKind.BloodWork, DiagnosticKind.Urinalysis, DiagnosticKind.FelvFivTest, DiagnosticKind.Imaging, DiagnosticKind.Other }
            .Single(k => k.DisplayName == kindName);

        Assert.Equal(expected, VisitBillingReader.DiagnosticDescription(kind, description));
    }

    [Fact]
    public void ALongStudyDescription_IsCutSoTheLineFitsAChargeDescription()
    {
        var text = VisitBillingReader.DiagnosticDescription(DiagnosticKind.Imaging, new string('x', 500));

        Assert.True(text.Length <= 300);
    }

    [Fact]
    public void LineKeys_AreStableAndWithinTheChargeLineRefLimit()
    {
        var id = Guid.NewGuid();
        var treatment = VisitBillingReader.TreatmentRefPrefix + id.ToString("N");
        var diagnostic = VisitBillingReader.DiagnosticRefPrefix + id.ToString("N");

        Assert.NotEqual(treatment, diagnostic);
        Assert.True(treatment.Length <= CatEcommerce.Shared.Common.Constants.ChargeRules.MaxSourceLineRefLength);
    }
}
