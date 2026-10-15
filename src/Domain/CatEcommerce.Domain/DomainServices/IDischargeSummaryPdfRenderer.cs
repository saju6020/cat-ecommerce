using CatEcommerce.Domain.Models;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-EMR-001 §8.8 / ADR-130 -- renders the <i>derived</i> discharge summary of a finalised visit to a
/// PDF (A4, text only, branch letterhead). The summary is never stored: a stored copy could disagree
/// with the immutable record and its addenda. Clinical free text is printed as plain text -- never
/// interpreted as markup. Implemented in Infrastructure with QuestPDF.
/// </summary>
public interface IDischargeSummaryPdfRenderer
{
    byte[] Render(DischargeSummaryDocument document);
}
