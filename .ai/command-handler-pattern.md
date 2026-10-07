# Command Handler Pattern Implementation Guide

The standard pattern for Commands, Validators and Command Handlers on the
`Platform.Infrastructure.*` libraries, with **in-memory dispatch in a single host**.

> **Two changes from the previous version of this document:**
>
> 1. **No CommandService layer.** The handler loads its aggregate from
>    `IAggregateRootRepository<TAggregate>` itself. A service is introduced only when a genuine
>    **Domain Service** is needed — see §5.
> 2. **No rejection event, no `202`.** The handler runs inside the request, so
>    `CommandResponse` becomes the HTTP response. ADR-005 is superseded by ADR-014.

---

## 1. Command Definition

Immutable, data only.

- Inherit `Platform.Infrastructure.Core.Commands.Command`.
- Use `required` and `init`.
- Located in `src/Application/Commands/[ContextName]/`.

```csharp
namespace CatEcommerce.Application.Commands.Cat;

public sealed class RecordVaccinationCommand : Command
{
    public required Guid CatId { get; init; }
    public required Guid VaccineId { get; init; }
    public required Guid BatchId { get; init; }
    public required DateOnly AdministeredOn { get; init; }
    public required Guid AdministeredByVetId { get; init; }
    public string? Notes { get; init; }
}
```

`Command` still carries `CorrelationId` and `UserContext`. Keep stamping them in the controller —
they remain how a request is traced through logs and how the audit trail attributes a change
(SRS FR-ACC-12), even though nothing is queued.

---

## 2. Command Validator

Shape validation only.

- Inherit FluentValidation's `AbstractValidator<TCommand>`.
- Registered as `IValidator<TCommand>` in `Program.cs`. The platform's `IValidationService` /
  `FluentValidationProvider` resolves it from DI — there is no `GetValidator()` override.
- Located in `src/Application/CommandHandlers/Validators/` (flat).

```csharp
public sealed class RecordVaccinationCommandValidator
    : AbstractValidator<RecordVaccinationCommand>
{
    public RecordVaccinationCommandValidator()
    {
        RuleFor(x => x.CatId).NotEmpty();
        RuleFor(x => x.VaccineId).NotEmpty();
        RuleFor(x => x.BatchId).NotEmpty();
        RuleFor(x => x.AdministeredOn)
            .LessThanOrEqualTo(_ => DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("A vaccination cannot be recorded with a future date.");
    }
}
```

---

## 3. Command Handler

The handler orchestrates: **load the aggregate, call one method on it, save it.**

- Implement `ICommandHandlerAsync<TCommand>` directly. There is no base class —
  `HandleAsync(TCommand)` returning `Task<CommandResponse>` is the entire contract.
- **Inject `IAggregateRootRepository<TAggregate>` and load the aggregate here.** Not through a
  service.
- Use a **primary constructor**.
- Located in `src/Application/CommandHandlers/[ContextName]/`.

```csharp
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Application.CommandHandlers.Cat;

public sealed class RecordVaccinationCommandHandler(
    ILogger<RecordVaccinationCommandHandler> logger,
    IAggregateRootRepository<CatAggregate> catRepository)
    : ICommandHandlerAsync<RecordVaccinationCommand>
{
    public async Task<CommandResponse> HandleAsync(RecordVaccinationCommand command)
    {
        logger.LogInformation(
            "Recording vaccination for cat {CatId}. CorrelationId: {CorrelationId}",
            command.CatId, command.CorrelationId);

        // 1. Load the aggregate — directly, no service layer.
        var cat = await catRepository.GetByIdAsync(command.CatId);
        if (cat is null)
            return Violation(
                $"Cat '{command.CatId}' was not found.",
                nameof(command.CatId),
                BusinessViolationCodes.CatNotFound);

        // 2. The aggregate enforces its own invariants and raises its own domain events.
        cat.RecordVaccination(command.ToDomainDto());

        // 3. Persist. EventPublishOption is REQUIRED — InMemoryPublishOnly publishes the
        //    aggregate's domain events in-process (Core 1.0.34). Never QueuePublishOnly:
        //    there is no queue.
        await catRepository.UpdateAsync(cat, EventPublishOption.InMemoryPublishOnly);

        return new CommandResponse { IsSuccess = true, Result = cat.Id };
    }

    private static CommandResponse Violation(string message, string property, string code)
    {
        var validationResponse = new ValidationResponse();
        validationResponse.AddError(
            errorMessage: message,
            propertyName: property,
            errorCode: code,
            resourceName: nameof(CatAggregate));

        return new CommandResponse { IsSuccess = false, ValidationResult = validationResponse };
    }
}
```

That is the whole handler. Compare it with the previous version, which injected an
`ICommandService`, an `IBusMessageDispatcher` and a repository, and published a rejection event on
every failure path.

### Where the violation goes now

Into the response body:

```
POST /api/cats/{id}/vaccinations
400 Bad Request
{ "errors": [ { "propertyName": "CatId", "errorCode": "CatNotFound",
                "errorMessage": "Cat '…' was not found." } ] }
```

No rejection event, no `INotifiedByEventHandler`, no notification subsystem, no polling. ADR-005
existed only because a `202` had already been returned and there was no response body left to
carry the violation. That constraint is gone.

---

## 4. A uniqueness check

Still a repository call, still in the handler:

```csharp
var exists = await catRepository.ExistAsync(c => c.MicrochipNumber == command.Microchip);
if (exists)
    return Violation($"A cat with microchip '{command.Microchip}' is already registered.",
                     nameof(command.Microchip), BusinessViolationCodes.ResourceAlreadyExists);
```

A uniqueness lookup is not domain logic and does not need a service to wrap it. If the same check
appears in three handlers, extract a method on the repository — not a service layer.

---

## 5. When a Domain Service **is** warranted

Only when the logic is **domain logic belonging to no single aggregate**, typically because it
must see several.

```csharp
namespace CatEcommerce.Domain.DomainServices;

/// FEFO allocation is a rule about the relationship between many batches.
/// No single BatchAggregate can express it.
public interface IStockAllocationService
{
    Task<IReadOnlyList<BatchAllocation>> AllocateAsync(Guid skuId, int quantity);
}
```

| Rule | Where it lives |
|---|---|
| "A cat cannot get the same vaccine twice on one day" | **`CatAggregate`** — its own state |
| "Next-due is 12 months after administration" | **`CatAggregate`** — its own data |
| "A stay cannot start if vaccinations are overdue" | **`CatAggregate`** exposes the status; the handler checks it |
| "Allocate from the soonest-expiring batch (FEFO)" | **Domain Service** — spans many batches |
| "Is this appointment slot free?" (roster + room + bookings) | **Domain Service** — spans three aggregates |
| "Does this microchip already exist?" | **Handler**, via repository. Not domain logic |

Rules for a Domain Service:

- Lives in `src/Domain/DomainServices/`, namespace `CatEcommerce.Domain.DomainServices`.
- Takes and returns **domain types**, never Commands or ViewModels.
- Depends only on repository interfaces — no `DbContext`, no HTTP client, no logger-driven flow.
- Is registered explicitly in `Program.cs` as Scoped.

**If you are about to create a service that only wraps one repository call, do not.** Put the call
in the handler.

---

## 6. Where each kind of rule lives

| Rule | Lives in |
|---|---|
| "CatId must not be empty" | Validator |
| "Administered date cannot be in the future" | Validator |
| "Cat must exist" | Handler — repository load, null check |
| "Microchip must be unique" | Handler — repository filter |
| "Cannot vaccinate twice with the same vaccine on the same day" | **Aggregate** |
| "Next-due date is 12 months out" | **Aggregate** |
| "Illegal status transition" | **Aggregate** — may throw |
| "Allocate stock FEFO" | **Domain Service** |

If a rule can be broken by looking only at the aggregate's own state, it belongs **in the
aggregate**. Moving it out is how a DDD model quietly turns anaemic — which is exactly what the
Command Service layer was doing.

---

## Summary of rules

1. **The handler loads and saves the aggregate itself**, through
   `IAggregateRootRepository<TAggregate>`. Verified contract:
   `GetByIdAsync(Guid)` · `GetByFilterAsync(Expression<Func<T,bool>>)` ·
   `ExistAsync(Expression<Func<T,bool>>)` · `SaveAsync(T, EventPublishOption)` ·
   `UpdateAsync(T, EventPublishOption)` · `UpdateAsync(T, int expectedVersion, EventPublishOption)`.
   Always pass **`EventPublishOption.InMemoryPublishOnly`**.
2. **No Command Service layer.** Domain Services only, and only for cross-aggregate domain logic.
3. **No manual input validation** — register `IValidator<TCommand>`; the platform runs it.
4. **Primary constructors** for DI.
5. **Implement `ICommandHandlerAsync<TCommand>` directly.** Build `CommandResponse` explicitly.
6. **All state mutation inside the aggregate.** The handler never sets aggregate state.
7. **One aggregate mutated per command.** Two is a signal the boundary is wrong.
8. **Violations return `CommandResponse { IsSuccess = false }`** — never thrown for control flow,
   never published as an event.
9. Register the handler and its validator in `Program.cs` — there is no assembly scanning.
