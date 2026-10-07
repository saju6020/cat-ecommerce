# The Api Host — a worked example

A complete vertical slice through the single host, from HTTP to database and back. This is the
shape every feature should match.

Feature: **record a vaccination for a cat** (SRS FR-CAT-5, FR-CAT-7).

---

## 1. `Program.cs`

The full host composition is in `.ai/host-composition.md`. The parts this slice adds:

```csharp
builder.Services.AddInMemoryBusServices();          // registers IInMemoryDispatcher (Scoped)
// AddServiceBusProvider(...) — REMOVED, no RabbitMQ

// The one seam the platform declares but does not fill (ADR-018):
builder.Services.AddScoped<IUserContextProvider, DevUserContextProvider>();

builder.Services.AddDbContext<CatEcommerceDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("CatEcommerce")));
builder.Services.AddScoped(typeof(IAggregateRootRepository<>), typeof(EfAggregateRootRepository<>));

builder.Services.AddScoped<ICommandHandlerAsync<RecordVaccinationCommand>,
                           RecordVaccinationCommandHandler>();
builder.Services.AddScoped<IValidator<RecordVaccinationCommand>,
                           RecordVaccinationCommandValidator>();
builder.Services.AddScoped<IQueryHandlerAsync<GetVaccinationHistoryQuery,
                                              IReadOnlyList<VaccinationHistoryItem>>,
                           GetVaccinationHistoryQueryHandler>();
```

---

## 2. Controller

Thin: mint the id, stamp the context, dispatch, map.

```csharp
namespace CatEcommerce.Api.Controllers;

[ApiController]
[Route("api/cats/{catId:guid}/vaccinations")]
public sealed class VaccinationController(IInMemoryDispatcher dispatcher) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Record(
        Guid catId, [FromBody] RecordVaccinationRequest request)
    {
        var command = new RecordVaccinationCommand
        {
            CatId               = catId,
            VaccineId           = request.VaccineId,
            BatchId             = request.BatchId,
            AdministeredOn      = request.AdministeredOn,
            AdministeredByVetId = request.AdministeredByVetId,
            Notes               = request.Notes,
            CorrelationId       = HttpContext.TraceIdentifier,
            UserContext         = HttpContext.GetUserContext()
        };

        var response = await dispatcher.SendAsync(command);

        return response.IsSuccess
            ? CreatedAtAction(nameof(GetHistory), new { catId }, response.Result)
            : BadRequest(response.ValidationResult);
    }

    [HttpGet]
    public async Task<IActionResult> GetHistory(Guid catId)
    {
        var response = await dispatcher.GetResultAsync(
            new GetVaccinationHistoryQuery { CatId = catId });

        return Ok(response.Result);
    }
}
```

What is **no longer** here, compared with the previous Api controller:

- No `IBusMessageDispatcher`, no `SendAsync` to a queue, no `QueueName` stamp.
- No `202 Accepted` — the response carries the created id.
- No manual `await validator.ValidateAsync(...)`: the platform's `IValidationService` runs the
  registered `IValidator<TCommand>` before the handler.
- No subscription to a notification channel to find out whether the command succeeded.

> **Verified against Core 1.0.34.** `IInMemoryDispatcher` exposes exactly
> `SendAsync(Command)`, `GetResultAsync<TResult>(IQuery<TResult>)` and
> `GetResult<TResult>(IQuery<TResult>)`. `PublishAsync` was removed in 1.0.33 — domain events are
> dispatched by the EF aggregate repository, not through the dispatcher (ADR-020). It deliberately
> has **no** `SendBusMessageAsync` or `PublishBusMessageAsync` — those live on `IDispatcher`, which
> is why controllers inject `IInMemoryDispatcher` instead (ADR-019).

---

## 3. Command + validator

```csharp
public sealed class RecordVaccinationCommand : Command
{
    public required Guid CatId { get; init; }
    public required Guid VaccineId { get; init; }
    public required Guid BatchId { get; init; }
    public required DateOnly AdministeredOn { get; init; }
    public required Guid AdministeredByVetId { get; init; }
    public string? Notes { get; init; }
}

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

## 4. Handler — loads the aggregate itself

```csharp
public sealed class RecordVaccinationCommandHandler(
    ILogger<RecordVaccinationCommandHandler> logger,
    IAggregateRootRepository<CatAggregate> catRepository)
    : ICommandHandlerAsync<RecordVaccinationCommand>
{
    public async Task<CommandResponse> HandleAsync(RecordVaccinationCommand command)
    {
        var cat = await catRepository.GetByIdAsync(command.CatId);
        if (cat is null)
        {
            var validation = new ValidationResponse();
            validation.AddError(
                errorMessage: $"Cat '{command.CatId}' was not found.",
                propertyName: nameof(command.CatId),
                errorCode: BusinessViolationCodes.CatNotFound,
                resourceName: nameof(CatAggregate));

            return new CommandResponse { IsSuccess = false, ValidationResult = validation };
        }

        cat.RecordVaccination(command.ToDomainDto());   // invariants + domain event inside

        // EventPublishOption is a required argument. InMemoryPublishOnly publishes the
        // aggregate's domain events in-process. Never QueuePublishOnly — there is no queue.
        await catRepository.UpdateAsync(cat, EventPublishOption.InMemoryPublishOnly);

        return new CommandResponse { IsSuccess = true, Result = cat.Id };
    }
}
```

**No CommandService, no `IBusMessageDispatcher` injected into the handler, no rejection event,
and `InMemoryPublishOnly` in place of `QueuePublishOnly`.** Three constructor parameters became
two, and the failure path became four lines.

---

## 5. Aggregate — where the rules actually live

```csharp
public class CatAggregate : AggregateRoot
{
    private readonly List<Vaccination> _vaccinations = new();
    public IReadOnlyList<Vaccination> Vaccinations => _vaccinations;

    public void RecordVaccination(RecordVaccinationDto dto)
    {
        // an invariant of THIS cat's own state — so it lives here, not in the handler
        if (_vaccinations.Any(v => v.VaccineId == dto.VaccineId
                                && v.AdministeredOn == dto.AdministeredOn))
            throw new InvalidOperationException(
                "This vaccine has already been recorded for this cat on this date.");

        var vaccination = Vaccination.Create(dto);
        _vaccinations.Add(vaccination);

        AddDomainEvent(new VaccinationRecordedEvent
        {
            CatId         = Id,
            VaccineId     = dto.VaccineId,
            NextDueOn     = vaccination.NextDueOn,
            CorrelationId = dto.CorrelationId,
            UserContext   = dto.UserContext
        });

        UpdateVersion();
    }
}
```

---

## 6. Query side

```csharp
public sealed class GetVaccinationHistoryQueryHandler(
    ILogger<GetVaccinationHistoryQueryHandler> logger,
    CatEcommerceDbContext db)
    : IQueryHandlerAsync<GetVaccinationHistoryQuery, IReadOnlyList<VaccinationHistoryItem>>
{
    public async Task<QueryResponse<IReadOnlyList<VaccinationHistoryItem>>> HandleAsync(
        GetVaccinationHistoryQuery query)
    {
        var items = await db.Cats
            .AsNoTracking()
            .Where(c => c.Id == query.CatId)
            .SelectMany(c => c.Vaccinations)
            .OrderByDescending(v => v.AdministeredOn)
            .Select(v => new VaccinationHistoryItem(
                v.VaccineId, v.AdministeredOn, v.NextDueOn))
            .ToListAsync();

        return new QueryResponse<IReadOnlyList<VaccinationHistoryItem>> { Result = items };
    }
}
```

The `GET` immediately after the `POST` returns the new row. No projection to wait for — that
immediacy is the practical gain from ADR-012.

---

## 7. What happens on dispatch, in order

```
POST /api/cats/{id}/vaccinations
  │
  ├─ IValidationService runs IValidator<RecordVaccinationCommand>   → invalid? 400
  ├─ IDispatcher resolves ICommandHandlerAsync<RecordVaccinationCommand> from DI
  ├─ handler: repository.GetByIdAsync → cat.RecordVaccination(...)
  │            → repository.UpdateAsync(cat, EventPublishOption.InMemoryPublishOnly)
  ├─ repository: SaveChangesAsync, then publishes the domain events in-process
  ├─ IEventHandlerAsync<VaccinationRecordedEvent> runs (recompute status, queue an SMS via outbox)
  └─ 201 Created { id }

  … seconds later, the outbox dispatcher delivers the SMS, out of band
```

Compare with the old path: `Api → RabbitMQ → CommandWorker → domain → event queue → EventWorker →
projection → notification → client`. Seven hops became one call stack.
