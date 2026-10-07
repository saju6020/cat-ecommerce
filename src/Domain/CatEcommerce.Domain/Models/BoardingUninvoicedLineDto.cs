using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Models;

/// <summary>
/// HO-BRD-001 §12.8 P2: <c>IBoardingBillableSource.GetUninvoicedLinesAsync</c>'s line shape --
/// "lines (<c>SourceLineId</c>, <c>Description</c>, <c>Amount</c>, <c>TaxClassId</c>,
/// <c>LineOfBusiness = Boarding</c>, <c>Kind</c>)". <c>LineOfBusiness</c> is fixed to
/// <see cref="BusinessLine.Boarding"/> per that contract's own note, so it is not a settable field
/// here. <c>Kind</c> reuses HO-BRD-001 §8.15's <see cref="BoardingStayChargeLineKind"/>.
/// </summary>
public sealed record BoardingUninvoicedLineDto(
    Guid SourceLineId,
    string Description,
    Money Amount,
    Guid TaxClassId,
    BoardingStayChargeLineKind Kind);
