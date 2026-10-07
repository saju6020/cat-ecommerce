# Domain Events and the Outbox

Domain events keep the platform contracts they always had — `Event`,
`AddDomainEvent(...)`, `IEventHandlerAsync<TEvent>`. What changed is that they are published
**in-process by the EF aggregate repository** instead of being queued to EventWorker.

| | **Domain event** | **Outbox message** |
|---|---|---|
| Raised by | An aggregate, inside its mutation method | A handler, for something leaving the process |
| Dispatched by | The EF aggregate repository, after the aggregate is saved | A background service, after commit |
| Runs | In-process, in the same request | Out of band, retried |
| Handler | `IEventHandlerAsync<TEvent>`, registered in `Program.cs` | The outbox dispatcher |
| Purpose | Side effects inside this system | SMS, email, courier, any external call |
| Example | Recompute a cat's vaccination status | Text the owner the next-due date |

---

## 1. Raising a domain event

Unchanged. The aggregate raises it as part of enforcing its own rules:

```csharp
public void RecordVaccination(RecordVaccinationDto dto)
{
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
```

## 2. Dispatching it

The repository dispatches the aggregate's collected events after `SaveChangesAsync`, resolving
`IEventHandlerAsync<TEvent>` from DI by each event's **runtime type** — see
`.ai/persistence-ef-core.md` §2 for the loop.

**The platform does not publish events for us.** `AddInMemoryBusServices()` registers no
`IEventPublisher` on Core 1.0.34, `IInMemoryDispatcher` has no `PublishAsync`, and
`IBusMessageDispatcher` is the RabbitMQ leg of a broker that is not deployed. This is host-owned
code (ADR-020).

**Resolve on the runtime type, never the static one.** `aggregate.Events` is
`ReadOnlyCollection<Event>`, so anything generic in the loop variable binds `TEvent = Event` and
finds no handler for the concrete event.

The handler passes **`EventPublishOption.InMemoryPublishOnly`**. The enum is retained —
`DoNotPublish=0`, `InMemoryPublishOnly=1`, `InMemoryAndQueuePublish=2`, `QueuePublishOnly=3` — and
only the last one is forbidden here, because there is no queue.

`AggregateRoot` exposes `ReadOnlyCollection<Event> Events` and `ClearEvents()`; the repository
dispatches from the first and calls the second.

## 3. Handling it

Unchanged contract, new registration site — `Program.cs` instead of EventWorker, and no
`EventConsumerAdapter`:

```csharp
namespace CatEcommerce.Application.DomainEventHandlers.Vaccination;

public sealed class RecalculateVaccinationStatusHandler(
    ILogger<RecalculateVaccinationStatusHandler> logger,
    IAggregateRootRepository<CatAggregate> catRepository)
    : IEventHandlerAsync<VaccinationRecordedEvent>
{
    public async Task HandleAsync(VaccinationRecordedEvent @event)
    {
        // side effect inside the same request
    }
}
```

```csharp
builder.Services.AddScoped<IEventHandlerAsync<VaccinationRecordedEvent>,
                           RecalculateVaccinationStatusHandler>();
```

### Rules

- A domain event carries **what happened**, past tense, with the data a handler needs. Not a
  command in disguise: `VaccinationRecorded`, never `UpdateVaccinationStatus`.
- Handlers live in `Application/DomainEventHandlers/[Feature]/`, **not** in the Read layer — they
  no longer build read models.
- **They are not for projections.** There is nothing to project into; a query reads the same
  tables.
- Register every one explicitly. There is no assembly scanning.
- A handler that must not fail the request should catch its own exceptions and record the failure.
  Decide per handler whether a failure should roll the command back, and say so in the handler's
  summary comment.

---

## 4. The outbox — anything leaving the process

External calls cannot join a database transaction. Sending an SMS inside the transaction risks
sending it and then rolling back; sending after commit risks committing and never sending.

The outbox resolves it: the **intent** to send is written to a table in the same transaction as
the business change, and a background service delivers it afterwards.

```
┌─ transaction ──────────────────────────────────────────┐
│  cat_vaccination row inserted                          │
│  cat row updated (next due date)                       │
│  outbox_message row inserted:                          │
│     type    = "SendVaccinationConfirmation"            │
│     payload = { ownerId, catName, nextDueOn }          │
└─ commit ───────────────────────────────────────────────┘
                     │
                     ▼
   OutboxDispatcher (BackgroundService in the Api host, polls)
                     │
                     ├─► SMS gateway ──► mark processed_on
                     └─► on failure: attempt_count++, backoff,
                         park in dead-letter after N attempts
```

```csharp
public sealed class OutboxMessage
{
    public Guid Id { get; init; }
    public string Type { get; init; } = default!;
    public string Payload { get; init; } = default!;   // jsonb
    public DateTimeOffset OccurredOn { get; init; }
    public DateTimeOffset? ProcessedOn { get; set; }
    public int AttemptCount { get; set; }
    public string? LastError { get; set; }
}
```

### Rules

- **Everything that leaves the process goes through it**: SMS, email, push, courier API,
  accounting export, and the payment provider when one arrives (SRS Phase 5).
- **Delivery is at-least-once.** The receiver must tolerate a duplicate, or the message must carry
  an idempotency key.
- **Never put a business decision in the payload.** It carries what to send, not what to decide.
  If the dispatcher has to work out whether to send, the decision belongs in the handler.
- The dispatcher is a `BackgroundService` in the Api host — registered with
  `AddHostedService<OutboxDispatcher>()`. It is the only asynchronous element left, and it exists
  for **delivery guarantees**, not decoupling.

---

## 5. Choosing between them

```
Does it leave the process (SMS, email, HTTP call)?
  └─ yes → outbox
  └─ no  → Must it happen as part of this command?
             └─ yes → domain event, handled in-process
             └─ no  → just call the code directly
```

That last branch matters. An event between two classes that always run together is indirection
with no benefit — it makes the flow harder to follow and the failure harder to trace. Use a direct
method call.
