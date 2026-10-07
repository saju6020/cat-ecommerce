using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Aggregates.Clinic;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Application.CommandHandlers.Clinic;

/// <summary>Shared <c>CommandResponse.Failure</c> shape for every Clinic Appointments handler --
/// the same pattern <c>CreateBranchCommandHandler.Violation</c> established for Administration.</summary>
public static class ClinicCommandHandlerSupport
{
    public static CommandResponse Violation(string code, string property, string message, string resourceName = "AppointmentAggregate")
    {
        var validationResponse = new ValidationResponse();
        validationResponse.AddError(errorMessage: message, propertyName: property, errorCode: code, resourceName: resourceName);
        return new CommandResponse { IsSuccess = false, ValidationResult = validationResponse };
    }

    /// <summary>A customer may act on an appointment only while it is theirs <em>and</em> they still
    /// own its cat (backlog #25): a cat ownership transfer only flags the appointment, so the
    /// previous owner's <c>CustomerId</c> still matches until staff act. A cat that can no longer be
    /// loaded does not lock the booking customer out of their own appointment.</summary>
    public static async Task<bool> IsCurrentOwnerAsync(
        IAggregateRootRepository<CatAggregate> catRepository, AppointmentAggregate appointment, Guid customerId)
    {
        if (appointment.CustomerId != customerId) return false;

        var cat = await catRepository.GetByIdAsync(appointment.CatId);
        return cat is null || cat.OwnerId == customerId;
    }

    public static CommandResponse Success(object? result = null) => new() { IsSuccess = true, Result = result! };
}

/// <summary>The controller-facing result of C1 (§12.4).</summary>
public sealed record AppointmentHoldResult(Guid AppointmentId, DateTimeOffset StartsOn, DateTimeOffset EndsOn, Guid? PerformerId, Guid? RoomId, DateTimeOffset HoldExpiresOn);
