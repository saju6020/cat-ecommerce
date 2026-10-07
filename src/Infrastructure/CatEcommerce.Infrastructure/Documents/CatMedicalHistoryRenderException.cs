namespace CatEcommerce.Infrastructure.Documents;

/// <summary>
/// ARCHITECTURE/CODE_REVIEW gates, LOW (2026-09-26, second pass). Port-level failure for
/// <see cref="ICatMedicalHistoryPdfRenderer.Render"/> -- <see cref="QuestPdfCatMedicalHistoryRenderer"/>
/// catches QuestPDF's own <c>DocumentDrawingException</c> and translates it into this type, so the
/// Api layer (<c>CatsController</c>) depends only on the port's own contract, never on which PDF
/// library sits behind it.
///
/// <see cref="Exception.Message"/> is deliberately generic and safe to log: QuestPDF's own
/// <c>DocumentDrawingException.Message</c> quotes the offending free text verbatim (a cat's name,
/// breed or note) -- see the SECURITY gate's LOW finding on the same failure path. The original
/// exception is kept as <see cref="Exception.InnerException"/> for diagnostics that need it, but
/// nothing in this solution logs it.
/// </summary>
public sealed class CatMedicalHistoryRenderException(Exception innerException)
    : Exception("The medical history document could not be rendered.", innerException);
