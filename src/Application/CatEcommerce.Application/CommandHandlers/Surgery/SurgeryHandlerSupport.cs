using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Surgery;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Application.CommandHandlers.Surgery;

/// <summary>
/// HO-SRG-001 -- the <c>CommandResponse.Failure</c> shape every Surgery handler shares (ADR-014: an expected outcome is RETURNED, never thrown).
/// The same pattern <c>ClinicCommandHandlerSupport</c> established for Appointments.
/// </summary>
public static class SurgeryHandlerSupport
{
    public static CommandResponse Violation(string code, string property, string message, string resourceName = "SurgeryAggregate")
    {
        var validationResponse = new ValidationResponse();
        validationResponse.AddError(errorMessage: message, propertyName: property, errorCode: code, resourceName: resourceName);
        return new CommandResponse { IsSuccess = false, ValidationResult = validationResponse };
    }

    public static CommandResponse Violation(DomainRuleViolationException ex, string resourceName = "SurgeryAggregate") =>
        Violation(ex.ErrorCode, ex.PropertyName, ex.Message, resourceName);

    /// <summary>Several violations in one 400 -- one per clash (US-SRG-01 "each of the three is checked and any clash is named").</summary>
    public static CommandResponse Violations(IEnumerable<(string Code, string Property, string Message)> violations, string resourceName = "SurgeryAggregate")
    {
        var validationResponse = new ValidationResponse();
        foreach (var (code, property, message) in violations)
            validationResponse.AddError(errorMessage: message, propertyName: property, errorCode: code, resourceName: resourceName);
        return new CommandResponse { IsSuccess = false, ValidationResult = validationResponse };
    }

    public static CommandResponse Success(object? result = null) => new() { IsSuccess = true, Result = result! };

    /// <summary>The surgery named by a route, or <c>null</c> when it does not exist OR belongs to another branch (HO-ADM-001 §8.2 / backlog #8: the same 404, never a reason that
    /// would confirm another branch id).</summary>
    public static async Task<SurgeryAggregate?> LoadSurgeryAsync(IAggregateRootRepository<SurgeryAggregate> repository, Guid surgeryId, Guid branchId)
    {
        var surgery = await repository.GetByIdAsync(surgeryId);
        return surgery is null || surgery.BranchId != branchId ? null : surgery;
    }

    public static CommandResponse TransitionViolation(SurgeryStatus current, string action)
    {
        try
        {
            SurgeryTransitionRules.EnsureAllowed(current, action);
            return Violation(BusinessViolationCodes.InvalidSurgeryTransition, action, "That change is not allowed now.");
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex);
        }
    }

    /// <summary>Whether the signed-in staff account holds the Vet role -- defence in depth behind the endpoint's <c>[RequireAction]</c> (BR-6).</summary>
    public static async Task<bool> IsVetAsync(IAggregateRootRepository<StaffAccountAggregate> staffRepository, Guid userId)
    {
        var caller = await staffRepository.GetByIdAsync(userId);
        return caller is not null && caller.RoleIds.Contains(StaffRoles.Vet);
    }
}
