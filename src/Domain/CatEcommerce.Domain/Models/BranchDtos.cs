using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

public sealed record OpeningIntervalInput(DayOfWeek DayOfWeek, TimeOnly Opens, TimeOnly Closes);

public sealed record BranchCreateDto(
    string Code, string Name, string TimeZoneId, string? AddressLine, string? PhoneNumber, string? Email,
    UserContext UserContext, Guid CorrelationId);

public sealed record BranchDetailsUpdateDto(
    string Name, string TimeZoneId, string? AddressLine, string? PhoneNumber, string? Email,
    UserContext UserContext, Guid CorrelationId);

public sealed record BranchWeeklyHoursDto(
    IReadOnlyList<OpeningIntervalInput> Intervals, UserContext UserContext, Guid CorrelationId);

public sealed record BranchActivationDto(bool IsActive, UserContext UserContext, Guid CorrelationId);
