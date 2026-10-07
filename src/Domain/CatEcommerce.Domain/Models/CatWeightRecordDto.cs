using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

public sealed record CatWeightRecordDto(
    Guid CatId,
    decimal Kilograms,
    DateOnly RecordedOn,
    Guid RecordedByStaffId,
    string? Notes,
    DateOnly Today,
    UserContext UserContext,
    Guid CorrelationId);
