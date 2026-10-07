using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

/// <summary>
/// IsCore and ValidityMonths travel from the (read-only) VaccineAggregate lookup; the
/// AGGREGATE computes NextDueOn — HO-CAT-001 §8.2.
/// </summary>
public sealed record CatVaccinationDto(
    Guid VaccinationId,
    Guid VaccineId,
    string VaccineCode,
    string VaccineName,
    bool IsCore,
    int ValidityMonths,
    string BatchReference,
    DateOnly BatchExpiresOn,
    DateOnly AdministeredOn,
    Guid AdministeredByVetId,
    string? Notes,
    DateOnly Today,
    UserContext UserContext,
    Guid CorrelationId);
