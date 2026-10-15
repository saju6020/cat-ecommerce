# HO-ORD-001-FU2 — Staff customer orders endpoint and courier tracking-URL validation (backend follow-up)

## 1. Identity

| Field | Value |
|---|---|
| Handover ID | **HO-ORD-001-FU2** (follow-up slice to [`HO-ORD-001`](HO-ORD-001.md) / [`HO-ORD-002`](HO-ORD-002.md)) |
| Date | 2026-10-12 |
| Branch | `ho/ORD-001-FU2` from master `b12fd39d` |
| Status | Backend: Complete. Not merged |

## 2. Objective

1. Build `GET /api/staff/customers/{customerId}/orders`, specified in HO-ORD-001 §12.4 (`Orders.View`, "the customer's recent orders, for HO-ACC-002's customer page") but never built. Without it the ORD-002 `CustomerOrdersPanel` (§11.14) guessed by searching the queue, which could show the wrong customer's orders.
2. Fix the backend half of ORD-002 SECURITY LOW: the courier `TrackingUrlTemplate` validator accepts only an absolute `http(s)` template.

**Owner decision (2026-10-12), verbatim:** "Build the endpoint".

## 3. Scope

No schema change, no migration, no write-side change beyond the validator.

- `src/Api/CatEcommerce.Api/Controllers/StaffCustomerOrdersController.cs` (new; `StaffOrderControllerBase`, `IInMemoryDispatcher`)
- `src/Read/CatEcommerce.Read/Queries/Order/StaffOrderQueries.cs` (`GetStaffCustomerOrdersQuery`)
- `src/Read/CatEcommerce.Read/QueryHandlers/Order/StaffOrderQueryHandlers.cs` (`GetStaffCustomerOrdersQueryHandler`: `AsNoTracking`, projected, paged)
- `src/Read/CatEcommerce.Read/ViewModels/StaffOrderViewModels.cs` (`StaffCustomerOrderRowViewModel`)
- `src/Api/CatEcommerce.Api/Modules/OrdersModule.cs` (handler registration)
- `src/Application/CatEcommerce.Application/CommandHandlers/Validators/OrderConfigurationCommandValidators.cs` (`CourierTrackingUrlRules`)
- `docs/handover/CONTRACTS.md` row 35 (authored as row 32; renumbered at merge 2026-10-13, EMR-001-F took 32-34)

## 4. Endpoint

`GET /api/staff/customers/{customerId}/orders?pageNumber&pageSize` — `[RequireAction(Orders.View)]` (Reception, Inventory, ShopManager, Accountant, Administrator; others `403`, anonymous `401`).

- **Scope rule applied:** the caller's **active branch** (`X-Branch-Id`), exactly like the queue and the detail (HO-ORD-001 §12.4: "an order of another branch does not exist for them"). The spec's one-line row does not say cross-branch, so none is returned. Orders of the customer at other branches are not listed.
- `404` when no customer account has that id; `200` with an empty page when the customer has no orders at this branch. A refused branch header is `403` (ADR-046).
- Newest first (`PlacedOn` desc, `Id`); default page 20, max 100; `pageSize<=0` is `400`.
- Response: `{ items, pageNumber, pageSize, totalCount }`; item = `{ id, orderNumber, placedOn, status, fulfilmentMethod, paymentMethod, channel, grandTotal: { amount, currency }, branchTimeZoneId }`.
- HO-ORD-002 §11.14 asks for number, date, status, total, a link (`id`) and a "new counter sale" link (built from `customerId`, no field needed). **Spec gap:** HO-ORD-001 does not define the row DTO or what "recent" means; this slice chose the paged list above (no date cut-off).

## 5. Courier tracking-URL template

`CreateCourierCommandValidator` / `UpdateCourierCommandValidator`: when present, the template (with `{tracking}` substituted) must parse as an absolute URI with scheme `http` or `https` and a host; `javascript:`, `data:`, `ftp:`, scheme-relative, relative and host-less forms fail on `TrackingUrlTemplate`. The aggregate still requires https and one `{tracking}`.

## 6. Tests

- `tests/CatEcommerce.Application.Tests/Validators/CourierTrackingUrlValidatorTests.cs`
- `tests/CatEcommerce.IntegrationTests/Endpoints/OrderingStaffCustomerOrdersEndpointTests.cs`: happy path and fields, another customer never returned, unknown customer `404`, branch scope, paging and bad page size, roles allowed / `403` / `401`.
- Run on `catecommerce_ordfu2` (per-branch database; never `catecommerce_dev`).
