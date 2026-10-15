using System.Net;
using CatEcommerce.Application.CommandHandlers.Operations;
using CatEcommerce.Application.CommandHandlers.Validators;
using CatEcommerce.Application.Commands.Operations;
using CatEcommerce.Application.DomainEventHandlers.Operations;
using CatEcommerce.Application.Tests.TestSupport;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Audit;
using CatEcommerce.Domain.Aggregates.Operations;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Application.Tests.CommandHandlers.Operations;

/// <summary>HO-XCT-001 U-B5, U-B9, U-B10, U-B11 and the C1..C4 validators.</summary>
public sealed class ErasureCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 10, 0, 0, TimeSpan.Zero);
    private static readonly UserContext Admin = new(Guid.NewGuid(), Guid.NewGuid()) { Roles = ["Administrator"] };

    private static ErasureRequestAggregate Ledger(Guid customerId, params string[] keys)
    {
        var ledger = new ErasureRequestAggregate(customerId);
        ledger.Open(new ErasureOpenDto(Now, ErasureRequestSource.SelfService, keys, Admin, Guid.NewGuid()));
        return ledger;
    }

    private static IServiceScopeFactory ScopeFactoryWith<T>(T service) where T : class =>
        new ServiceCollection().AddSingleton(service).BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

    private static CustomerAccountAggregate ActiveCustomer(FakeAggregateRootRepository<CustomerAccountAggregate> repository)
    {
        var customer = new CustomerAccountAggregate(Guid.NewGuid());
        customer.RegisterWalkIn(new WalkInCustomerRegistrationDto("Test Person", CatEcommerce.Domain.ValueObjects.MobileNumber.Create("01712345678"), Admin, Guid.NewGuid()));
        repository.Seed(customer);
        return customer;
    }

    // U-B5
    [Fact]
    public async Task U_B5_AnEraserThatThrows_LeavesTheScopeFailing_RecordedByCode_AndNothingEscapes()
    {
        var customerId = Guid.NewGuid();
        var ledgers = new FakeAggregateRootRepository<ErasureRequestAggregate>();
        ledgers.Seed(Ledger(customerId, "account"));
        var handler = new ExecuteErasureScopeCommandHandler(
            ledgers, [new FakeEraser("account", toThrow: new InvalidOperationException("boom with a personal value: Mrs Rahman"))],
            ScopeFactoryWith<IAggregateRootRepository<ErasureRequestAggregate>>(ledgers), new FakeOperationsClock(Now),
            NullLogger<ExecuteErasureScopeCommandHandler>.Instance);

        var response = await handler.HandleAsync(new ExecuteErasureScopeCommand
        {
            CustomerAccountId = customerId, ContextKey = "account", UserContext = Admin, CorrelationId = Guid.NewGuid()
        });

        Assert.True(response.IsSuccess);
        var scope = Assert.Single((await ledgers.GetByIdAsync(customerId)).Scopes);
        Assert.Equal(ErasureScopeStatus.Failing, scope.Status);
        Assert.Equal(ExecuteErasureScopeCommandHandler.UnhandledEraserException, scope.LastErrorCode);
        Assert.Equal(1, scope.Attempts);
        Assert.DoesNotContain("Rahman", scope.LastErrorCode);
    }

    [Fact]
    public async Task AnUnregisteredContextKey_IsFailedEraserNotRegistered()
    {
        var customerId = Guid.NewGuid();
        var ledgers = new FakeAggregateRootRepository<ErasureRequestAggregate>();
        ledgers.Seed(Ledger(customerId, "boarding"));
        var handler = new ExecuteErasureScopeCommandHandler(
            ledgers, [], ScopeFactoryWith<IAggregateRootRepository<ErasureRequestAggregate>>(ledgers), new FakeOperationsClock(Now),
            NullLogger<ExecuteErasureScopeCommandHandler>.Instance);

        await handler.HandleAsync(new ExecuteErasureScopeCommand { CustomerAccountId = customerId, ContextKey = "boarding", UserContext = Admin, CorrelationId = Guid.NewGuid() });

        Assert.Equal(ExecuteErasureScopeCommandHandler.EraserNotRegistered, (await ledgers.GetByIdAsync(customerId)).Scopes.Single().LastErrorCode);
    }

    [Fact]
    public async Task ACompletedScope_IsASuccess_AndTheEraserIsNotCalledAgain()
    {
        var customerId = Guid.NewGuid();
        var ledgers = new FakeAggregateRootRepository<ErasureRequestAggregate>();
        var ledger = Ledger(customerId, "account", "notification");
        ledger.RecordScopeOutcome(new ErasureScopeOutcomeDto("account", new ErasureOutcome.Completed(), Now, new DateOnly(2026, 10, 12), Admin, Guid.NewGuid()));
        ledgers.Seed(ledger);
        var eraser = new FakeEraser("account");
        var handler = new ExecuteErasureScopeCommandHandler(
            ledgers, [eraser], ScopeFactoryWith<IAggregateRootRepository<ErasureRequestAggregate>>(ledgers), new FakeOperationsClock(Now),
            NullLogger<ExecuteErasureScopeCommandHandler>.Instance);

        var response = await handler.HandleAsync(new ExecuteErasureScopeCommand { CustomerAccountId = customerId, ContextKey = "account", UserContext = Admin, CorrelationId = Guid.NewGuid() });

        Assert.True(response.IsSuccess);
        Assert.Equal(0, eraser.Calls);
        Assert.Empty(ledgers.Updated);
    }

    [Fact]
    public async Task AnEraserThatAnswersAHoldInThePast_IsRecordedAsAFailure_NotLost()
    {
        var customerId = Guid.NewGuid();
        var ledgers = new FakeAggregateRootRepository<ErasureRequestAggregate>();
        ledgers.Seed(Ledger(customerId, "billing"));
        var eraser = new FakeEraser("billing", new ErasureOutcome.Held(new DateOnly(2026, 10, 12), "D-9 invoice"));
        var handler = new ExecuteErasureScopeCommandHandler(
            ledgers, [eraser], ScopeFactoryWith<IAggregateRootRepository<ErasureRequestAggregate>>(ledgers), new FakeOperationsClock(Now),
            NullLogger<ExecuteErasureScopeCommandHandler>.Instance);

        await handler.HandleAsync(new ExecuteErasureScopeCommand { CustomerAccountId = customerId, ContextKey = "billing", UserContext = Admin, CorrelationId = Guid.NewGuid() });

        var scope = (await ledgers.GetByIdAsync(customerId)).Scopes.Single();
        Assert.Equal(ErasureScopeStatus.Failing, scope.Status);
        Assert.Equal(BusinessViolationCodes.InvalidHoldDate, scope.LastErrorCode);
    }

    // U-B9
    [Fact]
    public async Task U_B9_C3_AnActiveAccount_IsAnonymised_AndAuditedWithTheChannel()
    {
        var customers = new FakeAggregateRootRepository<CustomerAccountAggregate>();
        var audit = new FakeAggregateRootRepository<AuditEntryAggregate>();
        var customer = ActiveCustomer(customers);
        var handler = new EraseCustomerOnRequestCommandHandler(customers, audit, new FakeClientIpAccessor(IPAddress.Parse("10.0.0.1")), new FakeTimeProvider(Now));

        var response = await handler.HandleAsync(new EraseCustomerOnRequestCommand
        {
            CustomerAccountId = customer.Id, Channel = "phone", IdentityCheck = "registeredmobilecallback",   // lower case on purpose: the audit row must carry the canonical names
            UserContext = Admin, CorrelationId = Guid.NewGuid()
        });

        Assert.True(response.IsSuccess);
        Assert.False(((ErasureOnRequestResult)response.Result).IsReapplication);
        Assert.Equal(CustomerAccountStatus.Anonymised, customer.Status);
        var entry = Assert.Single(audit.Saved);
        Assert.Equal("CustomerErasureRequestedByStaff", entry.Action);
        Assert.Contains("Phone", entry.Reason);
        Assert.DoesNotContain("phone", entry.AfterValue);
        Assert.Contains("RegisteredMobileCallback", entry.AfterValue);
        Assert.Contains("RegisteredMobileCallback", entry.Reason);
        Assert.DoesNotContain("Customer asked", entry.Reason + entry.AfterValue);                  // no free text on the immutable audit row
        Assert.Equal(AuditCategory.BusinessEvent, entry.Category);
    }

    [Fact]
    public async Task U_B9_C3_AnAlreadyAnonymisedAccount_IsAReapplication_AndNothingIsWritten()
    {
        var customers = new FakeAggregateRootRepository<CustomerAccountAggregate>();
        var audit = new FakeAggregateRootRepository<AuditEntryAggregate>();
        var customer = ActiveCustomer(customers);
        customer.Anonymise(new CustomerAnonymisationDto(Now, Admin, Guid.NewGuid()));
        var handler = new EraseCustomerOnRequestCommandHandler(customers, audit, new FakeClientIpAccessor(), new FakeTimeProvider(Now));

        var response = await handler.HandleAsync(new EraseCustomerOnRequestCommand
        {
            CustomerAccountId = customer.Id, Channel = "Email", IdentityCheck = "InPersonPhotoId", UserContext = Admin, CorrelationId = Guid.NewGuid()
        });

        Assert.True(response.IsSuccess);
        Assert.True(((ErasureOnRequestResult)response.Result).IsReapplication);
        Assert.Empty(audit.Saved);
        Assert.Empty(customers.Updated);
    }

    [Fact]
    public async Task U_B9_C3_AMergedRecord_IsRefused_AndAnUnknownOneIsNotFound()
    {
        var customers = new FakeAggregateRootRepository<CustomerAccountAggregate>();
        var survivor = ActiveCustomer(customers);
        var duplicate = ActiveCustomer(customers);
        duplicate.MergeInto(new CustomerMergeDto(survivor.Id, "same person", survivor.MobileNumber, survivor.AdditionalPhoneNumbers, survivor.EmailAddress, Now, Admin, Guid.NewGuid()));
        var handler = new EraseCustomerOnRequestCommandHandler(customers, new FakeAggregateRootRepository<AuditEntryAggregate>(), new FakeClientIpAccessor(), new FakeTimeProvider(Now));

        var merged = await handler.HandleAsync(new EraseCustomerOnRequestCommand
        {
            CustomerAccountId = duplicate.Id, Channel = "Letter", IdentityCheck = "InPersonPhotoId", UserContext = Admin, CorrelationId = Guid.NewGuid()
        });
        var unknown = await handler.HandleAsync(new EraseCustomerOnRequestCommand
        {
            CustomerAccountId = Guid.NewGuid(), Channel = "Letter", IdentityCheck = "InPersonPhotoId", UserContext = Admin, CorrelationId = Guid.NewGuid()
        });

        Assert.Equal(BusinessViolationCodes.CustomerRecordMerged, merged.ValidationResult!.Errors.Single().ErrorCode);
        Assert.Equal(OperationsMessages.CustomerRecordMerged, merged.ValidationResult.Errors.Single().ErrorMessage);
        Assert.Equal(BusinessViolationCodes.CustomerAccountNotFound, unknown.ValidationResult!.Errors.Single().ErrorCode);
    }

    // U-B10
    [Fact]
    public async Task U_B10_C4_AnUnknownAccount_IsCounted_AndTheRestContinue()
    {
        var customers = new FakeAggregateRootRepository<CustomerAccountAggregate>();
        var first = ActiveCustomer(customers);
        var second = ActiveCustomer(customers);
        var handler = new ReapplyErasuresAfterRestoreCommandHandler(
            ScopeFactoryWith<IAggregateRootRepository<CustomerAccountAggregate>>(customers),
            new FakeAggregateRootRepository<AuditEntryAggregate>(), new FakeClientIpAccessor(), new FakeTimeProvider(Now),
            NullLogger<ReapplyErasuresAfterRestoreCommandHandler>.Instance);

        var response = await handler.HandleAsync(new ReapplyErasuresAfterRestoreCommand
        {
            CustomerAccountIds = [first.Id, Guid.NewGuid(), second.Id], RestoreReference = "restore 2026-10-12", UserContext = Admin, CorrelationId = Guid.NewGuid()
        });

        var result = Assert.IsType<ReapplyErasuresResult>(response.Result);
        Assert.Equal(2, result.Processed);
        Assert.Equal(1, result.NotFound);
        Assert.Equal(0, result.AlreadyErased);
        Assert.Equal(CustomerAccountStatus.Anonymised, first.Status);
        Assert.Equal(CustomerAccountStatus.Anonymised, second.Status);
    }

    [Fact]
    public async Task U_B10_C4_RunningItTwice_ReportsEveryAccountAsAlreadyErased()
    {
        var customers = new FakeAggregateRootRepository<CustomerAccountAggregate>();
        var customer = ActiveCustomer(customers);
        var handler = new ReapplyErasuresAfterRestoreCommandHandler(
            ScopeFactoryWith<IAggregateRootRepository<CustomerAccountAggregate>>(customers),
            new FakeAggregateRootRepository<AuditEntryAggregate>(), new FakeClientIpAccessor(), new FakeTimeProvider(Now),
            NullLogger<ReapplyErasuresAfterRestoreCommandHandler>.Instance);
        var command = new ReapplyErasuresAfterRestoreCommand { CustomerAccountIds = [customer.Id], RestoreReference = "r", UserContext = Admin, CorrelationId = Guid.NewGuid() };

        await handler.HandleAsync(command);
        var again = Assert.IsType<ReapplyErasuresResult>((await handler.HandleAsync(command)).Result);

        Assert.Equal(0, again.Processed);
        Assert.Equal(1, again.AlreadyErased);
    }

    [Fact]
    public void U_B10_TheValidator_Refuses501Ids_WithTheReapplyListTooLargeCode()
    {
        var validator = new ReapplyErasuresAfterRestoreCommandValidator();
        var tooMany = Enumerable.Range(0, RetentionRules.ReapplyMaxIds + 1).Select(_ => Guid.NewGuid()).ToList();

        var result = validator.Validate(new ReapplyErasuresAfterRestoreCommand { CustomerAccountIds = tooMany, RestoreReference = "r", UserContext = Admin, CorrelationId = Guid.NewGuid() });

        Assert.Contains(result.Errors, e => e.ErrorCode == BusinessViolationCodes.ReapplyListTooLarge);
        Assert.Empty(validator.Validate(new ReapplyErasuresAfterRestoreCommand
        {
            CustomerAccountIds = tooMany.Take(RetentionRules.ReapplyMaxIds).ToList(), RestoreReference = "r", UserContext = Admin, CorrelationId = Guid.NewGuid()
        }).Errors);
    }

    [Fact]
    public void TheC3Validator_RequiresAKnownChannelAndAFixedIdentityCheckCode()
    {
        var validator = new EraseCustomerOnRequestCommandValidator();
        var valid = new EraseCustomerOnRequestCommand
        {
            CustomerAccountId = Guid.NewGuid(), Channel = "Phone", IdentityCheck = "WrittenRequestSigned", UserContext = Admin, CorrelationId = Guid.NewGuid()
        };

        Assert.Empty(validator.Validate(valid).Errors);
        Assert.NotEmpty(validator.Validate(new EraseCustomerOnRequestCommand { CustomerAccountId = valid.CustomerAccountId, Channel = "Carrier pigeon", IdentityCheck = "InPersonPhotoId", UserContext = Admin, CorrelationId = Guid.NewGuid() }).Errors);
        Assert.NotEmpty(validator.Validate(new EraseCustomerOnRequestCommand { CustomerAccountId = valid.CustomerAccountId, Channel = "Phone", IdentityCheck = "", UserContext = Admin, CorrelationId = Guid.NewGuid() }).Errors);
        Assert.NotEmpty(validator.Validate(new EraseCustomerOnRequestCommand { CustomerAccountId = valid.CustomerAccountId, Channel = "Phone", IdentityCheck = "NID 1990123456, DOB 12/03/85, called back 01712345678", UserContext = Admin, CorrelationId = Guid.NewGuid() }).Errors);
    }

    // U-B11
    [Fact]
    public void U_B11_TheLedgerSource_IsDerivedFromTheCallerAndTheCorrelation()
    {
        var customer = new UserContext(Guid.NewGuid(), Guid.NewGuid()) { Roles = ["Customer"] };

        Assert.Equal(ErasureRequestSource.SelfService, OpenErasureRequestOnCustomerAnonymisedHandler.SourceFor(Guid.NewGuid(), customer));
        Assert.Equal(ErasureRequestSource.StaffOnBehalf, OpenErasureRequestOnCustomerAnonymisedHandler.SourceFor(Guid.NewGuid(), Admin));
        Assert.Equal(ErasureRequestSource.RestoreReconciliation, OpenErasureRequestOnCustomerAnonymisedHandler.SourceFor(RetentionCorrelation.NewReapply(), Admin));
        Assert.True(RetentionCorrelation.IsReapply(RetentionCorrelation.NewReapply()));
        Assert.False(RetentionCorrelation.IsReapply(Guid.NewGuid()));
    }
}
