using CatEcommerce.Application.Commands.Reporting;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Application.CommandHandlers.Reporting;

/// <summary>
/// HO-RPT-001 section 11.4 -- the <c>CommandResponse.Failure</c> shape the schedule handlers share (ADR-014: an expected outcome is returned, never thrown), and the checks that need another
/// aggregate: the report and its parameters, the branch (and its zone), and every recipient (section 8.9). Recipients are read, never mutated.
/// </summary>
public static class ReportScheduleSupport
{
    public const string Resource = "ReportScheduleAggregate";

    public static CommandResponse Violation(string code, string property, string message, string resourceName = Resource)
    {
        var validationResponse = new ValidationResponse();
        validationResponse.AddError(errorMessage: message, propertyName: property, errorCode: code, resourceName: resourceName);
        return new CommandResponse { IsSuccess = false, ValidationResult = validationResponse };
    }

    public static CommandResponse Violation(DomainRuleViolationException ex, string resourceName = Resource) => Violation(ex.ErrorCode, ex.PropertyName, ex.Message, resourceName);

    public static CommandResponse Success(object? result = null) => new() { IsSuccess = true, Result = result! };

    /// <summary>The time zone a schedule runs in: its branch's, or the main branch's when it covers every branch (section 8.9).</summary>
    public static async Task<TimeZoneInfo?> ResolveZoneAsync(IAggregateRootRepository<BranchAggregate> branches, Guid? branchId)
    {
        var branch = await branches.GetByIdAsync(branchId ?? AdministrationSeed.MainBranchId);
        if (branch is null) return branchId is null ? DefaultZone() : null;

        try { return TimeZoneInfo.FindSystemTimeZoneById(branch.TimeZoneId); }
        catch (TimeZoneNotFoundException) { return DefaultZone(); }
    }

    public static TimeZoneInfo DefaultZone() => TimeZoneInfo.FindSystemTimeZoneById(BillingRules.DefaultTimeZoneId);

    /// <summary>Catalogue membership, parameters, branch and the caller's cross-branch right. Returns the definition and zone, or the failure.</summary>
    public static async Task<(ReportDefinition? Definition, TimeZoneInfo? Zone, CommandResponse? Failure)> ResolveAsync(
        ReportScheduleCommandBase command, IReportCatalogue catalogue, IAggregateRootRepository<BranchAggregate> branches,
        IAggregateRootRepository<StaffAccountAggregate> staff, DateTimeOffset now)
    {
        var definition = catalogue.Find(command.ReportKey);
        if (definition is null)
            return (null, null, Violation(BusinessViolationCodes.UnknownReport, nameof(command.ReportKey), ReportingMessages.UnknownReport));

        var today = ReportPeriodResolver.LocalDate(now, DefaultZone());
        var (_, violation) = ReportFilterValidator.Validate(definition, today, today, null, command.Parameters);
        if (violation is not null && violation.Code is BusinessViolationCodes.InvalidReportParameter or BusinessViolationCodes.UnknownReportParameter)
            return (null, null, Violation(violation.Code, violation.Property, violation.Message));

        if (command.BranchId is null)
        {
            var caller = await staff.GetByIdAsync(command.UserContext!.UserId);
            if (caller is null || !ReportAccessPolicy.CanUseAllBranches(caller.RoleIds))
                return (null, null, Violation(BusinessViolationCodes.CrossBranchReportNotPermitted, nameof(command.BranchId), ReportingMessages.CrossBranchReportNotPermitted));
        }

        var zone = await ResolveZoneAsync(branches, command.BranchId);
        if (zone is null)
            return (null, null, Violation(BusinessViolationCodes.BranchNotFound, nameof(command.BranchId), "This branch does not exist."));

        return (definition, zone, null);
    }

    /// <summary>Each recipient: an account with an email address, that holds the report's category action, and (for a branch schedule) is assigned to that branch -- in that order (section 8.9).</summary>
    public static async Task<CommandResponse?> CheckRecipientsAsync(
        ReportDefinition definition, Guid? branchId, IReadOnlyList<Guid> recipientIds,
        IAggregateRootRepository<StaffAccountAggregate> staff, IAggregateRootRepository<StaffBranchAssignmentAggregate> assignments)
    {
        foreach (var recipientId in recipientIds)
        {
            var account = await staff.GetByIdAsync(recipientId);
            if (account is null)
                return Violation(BusinessViolationCodes.ReportRecipientNotPermitted, "recipientIds", ReportingMessages.ReportRecipientNotPermitted);

            if (account.EmailAddress is null)
                return Violation(BusinessViolationCodes.ReportRecipientHasNoEmail, "recipientIds", ReportingMessages.ReportRecipientHasNoEmail);

            // The decision is ReportAccessPolicy.CanReceive -- the one rule the detail read and the delivery guard use. The branches below only choose which message to give when it says no.
            IReadOnlyCollection<Guid> assigned = branchId is null ? [] : (await assignments.GetByIdAsync(recipientId))?.BranchIds.ToList() ?? [];
            if (ReportAccessPolicy.CanReceive(definition, account.RoleIds.ToList(), branchId, assigned)) continue;

            if (!ReportAccessPolicy.CanOpen(definition, account.RoleIds))
                return Violation(BusinessViolationCodes.ReportRecipientNotPermitted, "recipientIds", ReportingMessages.ReportRecipientNotPermitted);

            return branchId is null
                ? Violation(BusinessViolationCodes.ReportRecipientNotPermitted, "recipientIds", ReportingMessages.ReportRecipientNotPermittedAllBranches)
                : Violation(BusinessViolationCodes.ReportRecipientNotAssignedToBranch, "recipientIds", ReportingMessages.ReportRecipientNotAssignedToBranch);
        }

        return null;
    }
}
