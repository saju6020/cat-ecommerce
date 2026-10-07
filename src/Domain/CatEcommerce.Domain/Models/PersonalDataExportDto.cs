using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

public sealed record PersonalDataExportDto(DateTimeOffset ExportedOn, UserContext UserContext, Guid CorrelationId);
