using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

public sealed record SessionEndDto(DateTimeOffset EndedOn, SessionEndReason Reason, UserContext UserContext, Guid CorrelationId);
