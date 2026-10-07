# Enumeration Pattern

This project uses a custom `Enumeration` base class instead of C# `enum`. This provides richer behavior including display names, type safety, and clean persistence through an EF Core value conversion.

## Base Class

Provided by `Platform.Infrastructure.Core` — `Enumeration` is a platform base class, not
something this solution defines.

## How to Create a New Enumeration

```csharp
namespace {{RootNamespace}}.Shared.Common.Enums;

public class MyStatus(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly MyStatus Active = new(1, nameof(Active));
    public static readonly MyStatus Inactive = new(2, nameof(Inactive));
    public static readonly MyStatus Suspended = new(3, nameof(Suspended));
}
```

## Key Rules

1. **Use Primary Constructor** with `(int value, string displayName)` params.
2. **Static readonly fields** for each value — these are the "enum members".
3. **Use `nameof()`** for `displayName` to keep display names in sync with field names.
4. **Start values at 1**, not 0 (convention in this project).
5. **Namespace**: `{{RootNamespace}}.Shared.Common.Enums` (flat).
6. **File location**: `src/Shared/Common/Enums/[EnumName].cs`.

## Available API (from base class)

```csharp
// Get all values
IEnumerable<MyStatus> all = Enumeration.GetAll<MyStatus>();

// Lookup by integer value
MyStatus status = Enumeration.FromValue<MyStatus>(1); // Active

// Lookup by display name
MyStatus status = Enumeration.FromDisplayName<MyStatus>("Active");

// Comparison
bool isEqual = MyStatus.Active == someStatus;
int comparison = MyStatus.Active.CompareTo(MyStatus.Inactive);

// Properties
int value = MyStatus.Active.Value;       // 1
string name = MyStatus.Active.DisplayName; // "Active"
string str = MyStatus.Active.ToString();   // "Active"
```

## Persisting an Enumeration with EF Core

Store the **`Value`**, never the display name — display names are for humans and will be reworded.

```csharp
builder.Property(x => x.Status)
       .HasConversion(
           status => status.Value,
           value  => Enumeration.FromValue<AppointmentStatus>(value))
       .HasColumnName("status")
       .IsRequired();
```

Register it once as a convention if the same type appears on many entities:

```csharp
protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    => builder.Properties<AppointmentStatus>().HaveConversion<EnumerationConverter<AppointmentStatus>>();
```

Two consequences worth knowing:

- **A converted property cannot be used in a `Where` on the read side unless you compare the
  converted value.** `Where(x => x.Status == AppointmentStatus.Booked)` translates correctly;
  `Where(x => x.Status.DisplayName == "Booked")` does not, and will fail or fall back to
  client-side evaluation.
- **Read models usually map the raw `int` or a `string`,** not the `Enumeration` type. The read
  side exists to feed a screen, and a ViewModel carrying `"Booked"` is simpler than one carrying
  a domain type the Read layer is not allowed to reference.

## When to Use

- **Use `Enumeration`** for: status types, category types, or any domain concept with display
  names, behaviour, or a need to enumerate all values (`GetAll<T>()` drives a dropdown).
- **Use C# `enum`** for: internal-only flags or simple switches that carry no behaviour.

> The old guidance said "…and are stored in MongoDB". Persistence is no longer the deciding
> factor — an `Enumeration` persists cleanly via the conversion above. Choose on whether the
> concept has behaviour or needs display names.

## Existing Enumerations

Derived from the state machines in `docs/SRS.md` Appendix C.

| Type | Context | Values |
|---|---|---|
| `AppointmentStatus` | Appointment | Booked, CheckedIn, InConsultation, Completed, Cancelled, NoShow |
| `SurgeryStatus` | Surgery | Scheduled, PreOp, InTheatre, Recovery, Discharged, Cancelled |
| `TriagePriority` | Appointment | Routine, Urgent, Emergency |
| `PrescriptionStatus` | Prescription | Issued, Dispensed, Completed, Expired, Cancelled |
| `VaccinationStatus` | Cat | Current, DueSoon, Overdue |
| `BoardingStayStatus` | BoardingStay | Enquiry, Reserved, Confirmed, CheckedIn, CheckedOut, Cancelled, NoShow |
| `BoardingUnitType` | BoardingUnit | Standard, Deluxe, Family, Isolation |
| `IncidentSeverity` | BoardingStay | Low, Medium, High |
| `OrderStatus` | Order | PendingPayment, Confirmed, Packing, Dispatched, Delivered, Cancelled, Returned, Refunded |
| `PaymentMethod` | Invoice | CashOnDelivery, CashInStore, CardInStore, BankTransfer |
| `BatchStatus` | Batch | InTransit, Received, Available, Quarantined, Recalled, Expired, WrittenOff |
| `InvoiceStatus` | Invoice | Draft, Issued, PartPaid, Paid, Credited |
| `SubscriptionStatus` | Order | Active, Paused, Suspended, Cancelled |

> `PaymentMethod` deliberately has no online card or wallet value — payment gateway integration
> is deferred to Phase 5 (`docs/SRS.md` §10). Adding one before the gateway exists would let a
> command construct a state nothing can fulfil.
