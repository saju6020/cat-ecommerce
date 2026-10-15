using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-PHM-001 §8.7 (ADR-147) -- freezes the medicine label at the moment of dispensing. Every field of US-PHM-05 AC1 is here (cat, owner, medicine, dose
/// line, instructions, date, expiry); the expiry is the EARLIEST of the lines. The snapshot is stored on the dispensing record and a reprint prints it
/// unchanged -- a label never changes after the medicine leaves the counter. Pure.
/// </summary>
public static class MedicineLabelComposer
{
    public static MedicineLabelSnapshot Compose(MedicineLabelInput input)
    {
        var earliestExpiry = input.LineExpiries.Where(e => e is not null).Select(e => e!.Value).DefaultIfEmpty().Min();
        DateOnly? expiresOn = input.LineExpiries.Any(e => e is not null) ? earliestExpiry : null;

        return new MedicineLabelSnapshot(
            catName: input.CatName,
            ownerName: input.OwnerName,
            medicineLine: $"{input.MedicineName} {input.StrengthText}".Trim(),
            doseLine: DoseFormatter.Format(input.Dose, input.Route, input.Frequency, input.DurationDays),
            instructions: input.Instructions,
            dispensedOnLocal: input.DispensedOnLocal,
            expiresOn: expiresOn,
            quantityLine: DoseFormatter.QuantityLine(input.Quantity, input.DispensingUnit),
            prescriptionNumber: input.PrescriptionNumber,
            prescriberName: input.PrescriberName,
            clinicName: input.ClinicName,
            clinicPhone: input.ClinicPhone,
            clinicAddress: input.ClinicAddress,
            controlledNotice: input.IsControlled ? LabelRules.ControlledNotice : null,
            standardWarnings: LabelRules.StandardWarnings);
    }
}
