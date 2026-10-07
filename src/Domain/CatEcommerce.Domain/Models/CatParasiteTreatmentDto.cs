using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

public sealed record CatParasiteTreatmentDto(
    Guid TreatmentId,
    ParasiteTreatmentType TreatmentType,
    string ProductName,
    string? BatchReference,
    DateOnly AdministeredOn,
    Guid AdministeredByStaffId,
    int IntervalDays,
    string? Notes,
    DateOnly Today,
    UserContext UserContext,
    Guid CorrelationId);
