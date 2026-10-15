GATE: CODE_REVIEW (combined: architecture + security + code quality + test adequacy, per the gate scope rule for follow-ups)
HANDOVER: HO-ORD-001-FU2
COMMITS: 5d9f7920 (diff b12fd39d..5d9f7920)
DATE: 2026-10-12
STATUS: PASS

FINDINGS
- [MEDIUM] src/Application/CatEcommerce.Application/CommandHandlers/Validators/OrderConfigurationCommandValidators.cs:25 — the new validator is looser than the aggregate it fronts (CourierAggregate.ValidateTemplate already requires https, exactly one {tracking}, no userinfo, no whitespace/control chars). It accepts http, userinfo (https://u:p@host/{tracking}) and inner whitespace, so the tests' "http is accepted" case is only true at the validator; the command still fails with InvalidCourier from the domain. Net effect: ORD-69's protection is the domain rule, the validator adds only an earlier message. Align (https only, no UserInfo) or document that the domain is authoritative. Backlog ORD 99.
- [LOW] CourierAggregate.cs:46 / validator — `{tracking}` may sit in the authority (https://{tracking}.example/); the number is EscapeDataString'd so it cannot add a dot or slash, but the host label is staff-chosen. Require the placeholder after the host (path or query). Backlog ORD 100.
- [LOW] src/Read/CatEcommerce.Read/QueryHandlers/Order/StaffOrderQueryHandlers.cs:288 — two round trips, and a customer with a closed/anonymised account still returns 200 with their branch orders; confirm that is wanted for the staff page. Controller maps a null result to 404, which is only right if the dispatcher never nulls on error. Backlog ORD 101.
- [LOW] tests/CatEcommerce.IntegrationTests/Endpoints/OrderingStaffCustomerOrdersEndpointTests.cs — gaps: no test that a customer with orders at two branches sees only the caller's branch each way (the branch test only proves the other branch sees zero); no default page size 20 / clamp to 100 test; no 403 for a refused X-Branch-Id; no assertion of fulfilmentMethod, paymentMethod, channel, branchTimeZoneId. Backlog ORD 102.
- [LOW] tests/CatEcommerce.Application.Tests/Validators/CourierTrackingUrlValidatorTests.cs — no cases for whitespace, userinfo, IDN host, `{tracking}` twice or in the host, or an over-length template. Backlog ORD 103.

CHECKED, NO FINDING
- Conformance: HO-ORD-001 §12.4 row (Orders.View, the customer's orders) built; branch rule identical to the queue and detail (o.BranchId == active branch from TryResolveBranch, X-Branch-Id validated by IActiveBranchAccessor, 403 on refusal). IInMemoryDispatcher + GetResultAsync, StaffOrderControllerBase, handler registered explicitly in OrdersModule, AsNoTracking + Select projection + Skip/Take with a stable (PlacedOn desc, Id) order, PagingHelper bounds pageNumber (int overflow) and rejects pageSize<=0. Query is in Read, no aggregate rehydration, nothing written. No superseded-architecture constructs. No schema change.
- IDOR: both predicates (customer id, active branch) are applied in SQL; the branch comes from the validated header, never from the route or query string; the customer id is only a filter, so another customer's or another branch's order cannot be returned. Merged customers: ReassignCustomer repoints orders to the survivor, so the survivor's page lists them (correct).
- Role gating: RequireAction(Orders.View); test covers the five allowed roles, three refused, and anonymous 401.
- 404 vs 200 as a customer-existence oracle: ACCEPTABLE. Customer accounts are global, not branch-owned, and Customers.View staff (StaffCustomersController.Find by mobile/email) already resolve any account across branches. The id is an unguessable Guid, so no enumeration. The response carries no PII (no name/phone/email/address): number, date, status, methods, channel, total, time zone.
- DTO vs HO-ORD-002 §11.14 (number, date, status, total, link, "new counter sale" link from customerId): covered (orderNumber, placedOn, status, grandTotal, id). HO-ORD-001 defines no row DTO (recorded in the handover as a spec gap); CONTRACTS row 32 (renumbered 35 at merge 2026-10-13) is correct and additive.
- URL validator edge cases: `{tracking}` is substituted with "x" before parsing, so the braces cannot make a valid URL fail; Trim handles surrounding whitespace; IDN hosts parse through System.Uri and pass; javascript:, data:, ftp:, //host, relative and https:/// are refused. Every accepted template is checked again by the aggregate (https, userinfo, whitespace, one placeholder), so no unsafe scheme or credential form can be stored. Stored templates are already https by the domain rule, so nothing becomes invalid retroactively.
- Tests are real HTTP against a per-branch database; happy path, ordering, cross-customer isolation, 404, branch scope, paging and roles are meaningful. The 6 new endpoint tests pass.

PRE-EXISTING FAILURE RULING (filtered runs on catecommerce_ordfu2, never catecommerce_dev)
- WaitlistFlowTests.IG2: not reproduced. Run on its own on the FU2 branch it passes. The builder's failure was a shared-state or ordering flake in the full run; the diff touches no waitlist code.
- BillingTestGateDatabaseTests.XI_9: fails alone on the FU2 branch (21 rows) AND on unmodified master b12fd39d against the same database (20 rows). PRE-EXISTING, independent of this diff. The test regex-scans every text column of the whole database and matches audit.audit_entry.AfterValue text "ExpiresOn=##/##/#### ##:##:## PM +##:##" (a formatted date-time that looks like a card number once digits are masked); the count grows with what the shared database holds. It is a test false positive over accumulated data, not a stored card number. Backlog ORD 104.
- Not run: the full suite, and master against a clean scratch DB (master was run on catecommerce_ordfu2 only).

SCOPE CHECK
- files in allowed scope: 10/10 (handover, CONTRACTS row, controller, module registration, query/handler/viewmodel, validator, 2 test files)
- files outside allowed scope: none

Integrator note (merge 2026-10-13): this review's ORD backlog rows were authored as 84-89 and renumbered 99-104 at merge, because HO-ORD-002 had already numbered rows 63-98.
