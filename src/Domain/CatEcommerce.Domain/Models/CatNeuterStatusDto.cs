using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

public sealed record CatNeuterStatusDto(
    NeuterStatus NeuterStatus,
    DateOnly? NeuteredOn,
    DateOnly Today,
    UserContext UserContext,
    Guid CorrelationId);
