GATE: SECURITY
HANDOVER: HO-POR-001
COMMITS: a45a85e8 (branch ho/POR-001; diff a8761829..a45a85e8, the branch's own changes only)
DATE: 2026-10-10
STATUS: PASS

FINDINGS
- [LOW] src/Read/CatEcommerce.Read/QueryHandlers/Portal/Sections/PrescriptionsSectionReader.cs:20 — active prescriptions of a deceased cat are listed (Pharmacy's customer query does not filter LifeStatus; death does not cancel prescriptions), unlike the cats and visits sections. Own data only, no IDOR. Backlog POR 8.
- [LOW] src/Api/CatEcommerce.Api/Controllers/PortalController.cs:24 — no rate-limit policy on a fan-out read (about 30+ queries per call). The handover does not require one. Backlog POR 9.

CHECKED, NO FINDING
- AuthN/AuthZ: the only action, GET /api/portal/dashboard, carries [RequireCustomerSession]. Anonymous and staff sessions get 401 session_invalid with Cache-Control: no-store. GET only, so CSRF does not apply. Re-order is retired, so no write surface was added.
- IDOR: GetPortalDashboardQuery has no client-settable id and there are no route, query or body parameters. PortalCaller.CustomerId = UserContext.UserId (the same pattern as CustomerOrders/Billing/Appointments controllers). Every reader passes only the caller's id. Upstream scoping is verified: GetCatsQuery has OwnerId plus Alive. Vaccination status is read only for ids from that list. EMR GetCustomerCatVisitsQuery re-checks c.OwnerId == CustomerId && !IsMarkedToDelete and returns null otherwise (skipped). Pharmacy filters by the cat's current owner. Orders filters by o.CustomerId. Appointments filters by a.CustomerId. Billing uses CustomerSet (caller plus customers merged into them) and VisibleToCustomer.
- Transferred cats: every cat-derived read keys on the current OwnerId, so the previous owner loses visits and prescriptions after a transfer. Merged/anonymised accounts: MergeInto and Anonymise rotate SecurityStamp, and GetCurrentCustomerSessionQueryHandler requires IssuedForSecurityStamp == c.SecurityStamp, so their sessions die. Deceased cats are excluded from cats and visits; prescriptions are the exception (L1).
- Clinical minimisation (ADR-262): the portal ViewModels are portal-owned records mapped member by member. Visits expose id, cat, date, service, vet, hasSummary and followUpOn. EMR problem names are dropped. FollowUpOn is the date only (kind and appointment are dropped), and it was already customer-visible in the discharge summary. Prescriptions expose exactly catId, catName, activeCount and earliestEndsOn; medicine, dose, route, frequency, vet and repeat are dropped. The cat item drops HandlingNote.
- Orders: TrackingNumber comes from o.Shipment.TrackingNumber, which is already returned to the same customer by the order detail (OrderQueryHandlers.cs:80). No TrackingUrl, courier status, notes, staff fields or bank-transfer account are added to the list item or the portal item. CanReorder comes from the shared CustomerOrderRules, with no behavioural change to the detail endpoint. Balances carry the fixed howToPay text only, with no bank details (OQ-9).
- Error and existence leakage: there is no id input, so there is no 404 oracle. A failing section becomes {state: Unavailable, data: null} with no exception text in the body.
- Logs: the composer's single template logs section, exception and correlation id. CatsSectionReader logs only the correlation id. No names, amounts or clinical text are logged. OQ-4 (no access log for an owner's own reads) matches the owner decision.
- Caching: the 200 response sets Cache-Control: no-store, and so does the 401.
- No secrets, no new dependencies, no raw SQL, no deserialisation of input.

SCOPE CHECK
- files in allowed scope: 43/43 (portal files plus the additive EMR/ORD list-item edits, CONTRACTS, ADR and handover rows that §1 Amended (8) authorises)
- files outside allowed scope: none
