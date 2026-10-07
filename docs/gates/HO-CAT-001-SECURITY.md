GATE: SECURITY
HANDOVER: HO-CAT-001
COMMITS: pass 2 reviewed `git diff master...ho/CAT-001-gates` at head 67f5865. That covers 8519b06, ccc3aa2,
  103fa7b, 832bae4, 5e26340, d4eea55, 00967ce, cb2ddd2 and c529f8f; the master merge ac79f82, which brings in
  HO-ADM-001 phase A / ADR-043 from 5200d75; and 1294ffe, af20041 and 67f5865. The merged result was also
  reviewed as a whole for the cat and vaccine surface.
DATE: 2026-09-26
STATUS: PASS

HISTORY
- Pass 1: FAIL. There was 1 BLOCKER: no authorization on the cat and vaccine endpoints. There were also 2 HIGH,
  4 MEDIUM and 2 LOW. The pass-1 record is kept below.
- Pass 2 (this entry): PASS. Every pass-1 BLOCKER, HIGH and MEDIUM finding is closed and was re-proven by live
  probe. Two items are carried and are not findings of this gate:
  - the photo StorageKey trust issue, which goes to HO-FIL-001;
  - read-auditing of clinical records, which stays LOW.
  Three new LOW findings are listed below. None of them blocks the gate.

HOW IT WAS CHECKED (pass 2)
- Code read:
  - CatsController and VaccinesController as merged;
  - RequireActionFilter, OwnerScope and CustomerActions;
  - ActionRoles.Administration.cs (the Cats and Vaccines rows);
  - GetCatOwnerQueryHandler;
  - the vaccination, parasite, register and transfer handlers;
  - all cat and vaccine validators;
  - NulByteRejectionFilter;
  - GetAuditEntriesQueryHandler and ClinicalAuditEntityTypes(.CatProfiles);
  - the AdminAuditEntriesController view and CSV export;
  - QuestPdfCatMedicalHistoryRenderer with the Bengali fallback font;
  - migration 20260926135821_WidenAuditEntryValues, which is AlterColumn only, with no raw SQL.
- Scratch setup:
  - fresh `git archive 67f5865` into `scratchpad\cat1-sec2`, with no git-ignored appsettings;
  - fresh DB `catecommerce_cat1sec`, created with `dotnet dotnet-ef database update --connection ...`. All
    migrations applied, including WidenAuditEntryValues;
  - `CATECOMMERCE_TEST_DB` set to the full connection string.
- Suites, all green on the fresh DB:

  | Suite | Result |
  |---|---|
  | Domain | 223/223 |
  | Application | 87/87 |
  | Infrastructure | 28/28 |
  | Integration | 327/327 |
  | web vitest | 37 files, 104/104 |

  The web tests ran in the clean worktree at 67f5865 because the scratch copy has no node_modules.
- Probes:
  - `CatSecurityGate2ProbeTests.cs` (S1 to S8), run through `AccountsApiFactory` with `DevUser:Enabled=false`.
  - Customers are registered and signed in through the real endpoints.
  - Staff are seeded, then signed in through `/api/staff/auth/sign-in` with the real CSRF header.
  - 8/8 ran. The probe file and raw output are kept in `scratchpad\cat1-sec2-probes\`.
  - Nothing was added to the worktree.

PASS-1 FINDINGS, RE-PROBED
- [BLOCKER, CLOSED] No authorization on the cat and vaccine endpoints.
  - S1: an anonymous client with no cookies called all 20 cat and vaccine endpoints. Every one returned 401
    `session_invalid` (20/20). The victim's cat was unchanged.
  - Every action carries `[RequireAction]`.
- [HIGH, CLOSED] Customer IDOR on catId, and US-CAT-08 AC2. Probe S2, customer B against customer A's cat:
  - Owner-scoped actions return 404, with no existence oracle. These were View, EditProfile, photos, weights
    GET, vaccination-status and the PDF.
  - Staff-only actions return 403. These were neuter, medical-profile, high-risk set and clear, weights POST,
    vaccinations, parasite, deceased, transfer, cage-card and both vaccine endpoints.
  - An unknown id returns 404, and a malformed id returns 404 at the route.
  - `GET /api/cats?ownerId=A` called by B returns only B's cats. The ownerId is forced (F19).
  - After a Reception transfer from A to B, A gets 404 on GET, weights, vaccination-status, the PDF and the
    profile PUT, and A's list is empty. B (the new owner) gets 200. AC2 now holds on every path.
- [HIGH, CLOSED] Staff role separation, probe S3. All of the following were refused with 403:
  - Accountant marking a cat deceased, or viewing a cat;
  - ShopManager registering a vaccine;
  - Caretaker recording a weight;
  - Reception editing the medical profile;
  - Nurse marking deceased, or transferring ownership.

  Caretaker printing a cage card is allowed (200), as the ADM-001 §12.3 table says.
- [MEDIUM, CLOSED] Mass assignment of identity fields, probes S2 and S4:
  - Customer register with `ownerId` = A: the cat is owned by the caller (B), not A.
  - Weight `recordedByStaffId` is no longer in the contract. A spoofed value was ignored, and the stored row
    carries the caller's (the Nurse's) id.
  - `administeredByVetId` gives these results:

    | Value sent | Result |
    |---|---|
    | random Guid | 400 StaffAccountNotFound |
    | a customer id | 400 StaffAccountNotFound |
    | a Nurse | 400 StaffAccountNotVeterinarian |
    | empty | 400 |
    | a real Vet | 201 |

  - `administeredByStaffId`: a customer id or a random Guid → 400, and a real staff colleague → 201. That
    matches §10.2: any staff member may deworm.
- [MEDIUM, CLOSED] Reason had no maximum length, probe S6:
  - a 1001-character reason → 400 MaximumLengthValidator;
  - a 1000-character reason with a 500-character handling note → 200.
- [MEDIUM, CLOSED] The audit snapshot overflowed its column. `audit_entry.before_value` and `after_value` are
  now `text`. Probe S6: medical-profile notes at the 1000-character validator maximum → 200.
- [MEDIUM, CARRIED to HO-FIL-001] The photo StorageKey is client-supplied. This is not a finding here, per the
  task. A NUL in the key or filename is now rejected (400).
- [LOW, CLOSED] The Bengali PDF crash. Probe S7: cats named `Mishti ঝুমুর` and `শিমুল বিড়াল`, with a
  Bengali handling note and a Bengali product name → 200 application/pdf, about 64 KB each.
  - A render failure now logs only the cat id, never the exception message.
- [LOW, CARRIED] Clinical reads are not access-logged. This is not a finding here, per the task.

NEW FINDINGS (pass 2)
- [LOW] src/Api/CatEcommerce.Api/Controllers/CatsController.cs `ApplyPaging` and VaccinesController.cs
  `GetVaccines`: there is no upper bound on `pageNumber`.
  - The code clamps `pageSize` to `PagingRules.MaxPageSize` and rejects values ≤0. It does not apply the
    `PagingRules.MaxPageNumber` guard that AdminAuditEntriesController already uses.
  - Proven (S5), as a signed-in customer and as a Nurse: `pageNumber=2147483647` returns 500 on
    `GET /api/cats`, `GET /api/cats/{id}/weights` and `GET /api/vaccines`. The same happens with
    `pageSize=100`.
  - `pageNumber=21474837&pageSize=100` → 200, which points to an int overflow of `(pageNumber-1)*pageSize` in
    the base Skip.
  - The 500 body is empty, so no data leaks. Impact: any authenticated customer can trigger unhandled errors
    and error-log noise at will.
  - Remediation: in both controllers, return 400 when `pageNumber > PagingRules.MaxPageNumber`, as
    AdminAuditEntriesController does. Add a CatsPaginationEndpointTests case for it.
- [LOW] src/Application/.../CommandHandlers/Cat/RegisterCatCommandHandler.cs and
  TransferCatOwnershipCommandHandler.cs never check that a staff-supplied owner id belongs to a real customer.
  - Proven (S4), as Reception:
    - registering with a random `ownerId` → 201. The cat is orphaned, and no customer can ever see it.
    - transferring a real customer's cat to a Nurse's staff-account id → 200. The customer loses the cat, and
      it now "belongs" to a staff id.
  - This is limited to Reception and Administrator, and each change is audited with the real actor. It is an
    integrity weakness, not a privilege escalation.
  - Remediation: resolve `OwnerId`/`NewOwnerId` against `CustomerAccountAggregate`, excluding soft-deleted
    accounts, and return 400 CustomerNotFound otherwise. RecordCatVaccinationCommandHandler already follows
    this pattern for the vet id.
- [LOW] src/Application/.../Validators/UpdateCatMedicalProfileCommandValidator.cs has no count cap on the
  Allergies, ChronicConditions or Medications lists.
  - Now that the audit columns are unbounded `text`, the incidental cap that pass 1 noted is gone.
  - Proven (S6), as a Vet: 3000 allergies of 500 characters each (about 1.5 MB) → 200 in about 1.7 s.
  - After that, every later write to the cat puts the whole owned graph into the immutable `audit_entry`
    table, as both before and after values. That is roughly 3 MB of undeletable audit data per write.
  - Clinical staff can do this; customers cannot.
  - Remediation: add a count cap for each list in the validator, for example `.Must(l => l.Count <= 50)`.

CHECKED, NO FINDING (pass 2)
- CSRF: a signed-in customer without `X-CSRF-Token` → 403 `csrf_token_invalid`. It now covers the cat
  endpoints, because anonymous callers stop at 401.
- RequireActionFilter fails closed:
  - an OwnerScope.Cat action with a missing or unparsable catId → 403, not allow;
  - a customer calling an action outside CustomerActions.Cat → 403 with a denial audit row;
  - the owner lookup reads `db.Cats` by id, so an unknown cat gives the same 404 as a cat owned by someone else.
- Paging, apart from the finding above:
  - `pageSize` ≤0 → 400, in the standard ValidationResult shape;
  - `pageSize=100000` is clamped to 100, and `totalCount` is returned;
  - `pageNumber` ≤0 → page 1;
  - non-numeric values and values above int range → 400 from model binding.
- NUL and control characters, overlong strings and enum abuse:
  - The global NulByteRejectionFilter rejected a NUL in every cat and vaccine string field tried: name,
    breed, colour, species, the sex enum string, profile, medical notes, allergen, handling note, reason,
    neuter enum, weight notes, product, photo key and filename, vaccine code/name/description, transfer
    reason and deceased note. Each → 400.
  - Other C0 characters (BEL, ESC) in a name are accepted (201). There is no SQL, header or PDF sink for them:
    Content-Disposition escapes them, and the PDF prints them as text.
  - Every length maximum holds, for example name 101 → 400, while a 100-character Bengali name → 201.
  - Enums are exact DisplayName allow-lists: a numeric, empty, null, unknown or NUL-suffixed value → 400.
  - The weight range and future dates → 400, `1e30` → 400, and `intervalDays=int.MaxValue` → 400.
- PDF and cage card:
  - The owner gets 200 `application/pdf`. Another customer gets 404, and so does the previous owner after a
    transfer. The owner gets 403 on the cage card, which is staff-only. Staff get 200 `image/png`.
  - The PDF sends `Content-Disposition: attachment`. The filename is ASCII-sanitised, with RFC 5987
    `filename*`.
  - Header injection attempts were neutralised: CRLF + `Set-Cookie`, `"; filename=evil.exe`,
    `../../etc/passwd` and a U+202E RTL override. None produced a Set-Cookie header, and there is no second
    filename parameter.
  - QuestPDF `.Text()` has no markup sink. The QR payload is only `/cats/{id}`.
  - Hardening note, not a finding: neither the PDF nor the cage-card PNG sends `Cache-Control: no-store`. This
    is host-wide behaviour for authenticated GETs, not specific to cats.
- Audit rows for clinical changes (S8):
  - Each vet write produced a DataChange row on CatAggregate, attributed to the Vet: register (attributed to the
    owner), medical-profile, flag and deceased. Vaccinations and parasite treatments are owned by CatAggregate,
    so they audit under it.
  - The flag also produced a BusinessEvent row (`Cat`, CatFlaggedHighRisk) with the actor and reason.
  - A weight wrote a CatWeightRecordAggregate row attributed to the Vet. No actor is `Guid.Empty` any more.
- Redaction of clinical values in audit views (HO-ADM owner decision). Tested with marker strings (`SECRET*`) in
  the allergen, diet, handling note, reason, weight note and deceased note:
  - Accountant (AuditLog.View, no Cats.View), on the JSON view filtered by cat, the view filtered by
    `entityType=CatWeightRecordAggregate`, the unfiltered view and export.csv: no marker leaked, and the
    `[Redacted -- clinical record, requires Cats.View]` placeholder was present.
  - Administrator, and a staff member holding both Accountant and Vet (Cats.View): values visible, as decided.
  - A customer on `/api/admin/audit-entries` → 403.
- SQL injection: none. The code is all LINQ and parameterised EF, and the new migration is AlterColumn only.
- Logs: the PDF failure path logs only the cat id. The query and command handlers log ids and flags only.

SCOPE CHECK (pass 2)
- Files in allowed scope: 60/76 in `git diff master...ho/CAT-001-gates`. These are the HO-CAT-001 §13
  src/tests paths plus its gate-fix bundle.
- Files outside allowed scope:
  - the integrator-authorised migration `20260926135821_WidenAuditEntryValues` (.cs, .Designer.cs and the
    snapshot). It was authorised per the parallel-build plan, and it widens columns only;
  - AdministrationIntegrationTestBase.cs and AdminGlobalSettingsLiveEffectEndpointTests.cs, the post-merge test
    fix 1294ffe;
  - 11 `web/` files, the PagedResult client follow-up. They consume the API only and add no security surface;
  - 5 docs files, which are gate records and handover amendments.
  None of these adds any security surface beyond what is reviewed above.

================================================================================================
PASS 1 RECORD (kept verbatim below)
================================================================================================

GATE: SECURITY
HANDOVER: HO-CAT-001
COMMITS: 4007225 (the original landing), 110913c (merge of the C10 amendment: 1462bb6 4012393 39254c0 ca43062), reviewed as they stand on master 7abc9e4
DATE: 2026-09-26
STATUS: FAIL

HISTORY
- Pass 1 (this file): FAIL. This is the first SECURITY run on HO-CAT-001. The code landed on 2026-09-21
  with no gates. It was reviewed against master 7abc9e4, which also has HO-ACC-001's sessions, CSRF,
  `[RequireAction]`, SafeExceptionHandlerMiddleware, the CORS allow-list and audit capture.

HOW IT WAS CHECKED
- Read CatsController, VaccinesController, the 13 cat and vaccine commands with their validators,
  handlers, mapper and audit event handlers, the 7 query handlers, CatAggregate.TransferOwnership,
  the Cat, Audit and Vaccine EF configurations, `20260920110910_AddCatProfile`, the PDF renderer and
  QR generator, and CatProfilesModule. Also read the HO-ACC-001 pieces they now sit behind:
  SessionAuthenticationMiddleware, SessionUserContextProvider, RequireActionFilter, ActionRoles,
  AuditChangeCaptureInterceptor and Program.cs.
- Migrations applied to `catecommerce_cat` with `dotnet dotnet-ef database update`.
- Probes ran in a fresh `git archive` of 7abc9e4 in the scratchpad (`catsec/`), against
  `catecommerce_cat`, through `AccountsApiFactory`. The factory uses the Testing environment with
  `DevUser:Enabled=false`, so an anonymous caller really is `Guid.Empty`. Customers are registered
  and signed in through the real endpoints. Staff are seeded as in StaffEndpointTests and signed in
  through `/api/staff/auth/sign-in`. Probe files: `CatSecurityGateProbeTests.cs` (P1 to P5) and
  `CatSecurityGateProbe2Tests.cs` (P6, captured logs). 6/6 ran. None were added to the worktree.

FINDINGS
- [BLOCKER] src/Api/CatEcommerce.Api/Controllers/CatsController.cs (all 17 actions) and
  VaccinesController.cs (both actions): no authorization of any kind.
  - Rule broken: SRS NFR-SEC-2 (server-side authorization on every request) and NFR-SEC-8 (clinical
    records limited to clinical roles and the owning customer).
  - What the code does:
    - no action carries `[RequireAction]`, `[RequireCustomerSession]` or any other filter;
    - no query handler or command handler compares `UserContext.UserId` with `cat.OwnerId`;
    - the host has no fallback authorization policy.
  - Proven (P1, a client with no cookies at all):
    - `GET /api/cats` with no `ownerId` → 200, every owner's cats with their `ownerId`.
      `pageSize=100000` returned the whole table (7 of 7 rows at that moment).
    - `GET /api/cats/{id}` → 200: the full clinical profile, allergies, medications, notes and photo
      keys.
    - `PUT .../medical-profile` → 200 (overwrote a real customer's allergy list).
    - `PUT .../high-risk-flag` → 200.
    - `POST .../weights` → 201.
    - `POST .../deceased` → 200.
    - `POST .../ownership-transfer` to a random Guid → 200. The victim's own list is then `[]`.
    - `POST /api/vaccines` → 201: a vaccine is added to the clinic catalogue.
    - `GET .../medical-history.pdf` → 200 and `.../cage-card` → 200.
  - Every one of these writes is audited with actor `00000000-0000-0000-0000-000000000000`, so the
    trail cannot say who did it.
  - Context: HO-CAT-001 §4.2 and §20.3 deliberately deferred authorization, because no identity
    existed yet. That precondition no longer holds: HO-ACC-001 is merged. The code sits on master
    with no guard.
  - Remediation: land HO-ADM-001 §12.3's CatsController/VaccinesController edits now, as a focused
    change ahead of the rest of ADM-001:
    - add `Actions.Cats.*` and `Actions.Vaccines.*`, plus `ActionRoles.CatProfiles.cs` with a
      `CatProfilesRows` field taken from the ADM-001 §12.3 table;
    - put `[RequireAction]` on every action;
    - add the customer owner-scope check, which returns 404 for a cat that is not the caller's.
  - Until then, the cat and vaccine controllers must not be reachable in any deployed environment.
- [HIGH] CatsController.cs: customer IDOR on `catId`. US-CAT-08 AC2 ("the previous owner loses
  access") does not hold.
  - What the code does: ownership is only a filter, and only on `GET /api/cats?ownerId=`, where the
    caller picks the `ownerId`.
  - Proven (P2, two signed-in customers A and B, each with a valid CSRF header):
    - B lists A's cats → 200.
    - B reads A's cat → 200.
    - B renames it → 200.
    - B transfers it to B → 200. A customer can steal any cat.
  - After that transfer, A (the previous owner):
    - `GET /api/cats?ownerId=A` → the cat is absent. This is the only AC2 behaviour; it is what
      I15 tests.
    - `GET /api/cats` unfiltered → the cat is present.
    - `GET /api/cats/{id}` → 200.
    - `GET .../vaccination-status` → 200; `.../weights` → 200; `.../medical-history.pdf` → 200.
    - `PUT .../profile` → 200.
    - `POST .../ownership-transfer` back to A → 200.
  - AC2 therefore fails on every read and write path except the one list filter.
  - Remediation: this is the owner-scope half of the BLOCKER fix.
    - Resolve `cat.OwnerId` for every `{catId}` route and compare it with `UserContext.UserId` for
      customer callers. Not owned or not found → 404.
    - For customers, ignore any `ownerId` query parameter and force it to the caller.
    - Transfer must be staff-only (ADM-001: Reception, Administrator).
    - Add an integration test for "previous owner gets 404 on GET /api/cats/{id} after transfer".
- [HIGH] src/Shared/CatEcommerce.Shared/Common/Constants/: there is no `ActionRoles.CatProfiles.cs`
  and no `Actions.Cats`, so staff role separation does not exist for cats.
  - Proven (P3):
    - a staff member holding only `Accountant` marks a cat deceased → 200;
    - a staff member holding only `ShopManager` registers a vaccine → 201.
  - Proven (P2): a customer sets the high-risk flag, a clinical-staff action, on another customer's
    cat → 200.
  - Remediation: part of the BLOCKER fix. Map every action and rely on `ActionRoles` failing closed.
- [MEDIUM] CatsController.cs:36, :230, :270, :305: mass assignment of identity fields.
  - `RegisterCat` binds `OwnerId` from the body.
    - Proven (P2): customer B registers "Planted by B" with `ownerId=A` → 201, and it appears in A's
      list.
  - `RecordedByStaffId`, `AdministeredByVetId` and `AdministeredByStaffId` are client-supplied, so
    clinical records carry a caller-chosen clinician.
    - Proven (P1): an anonymous weight record with a random `recordedByStaffId` → 201.
  - Remediation:
    - for customer callers, force `OwnerId = UserContext.UserId` (ADM-001 F19);
    - for staff, take the recording clinician from `UserContext.UserId`, or check the supplied id
      against a staff account holding the required role.
- [MEDIUM] Validators/FlagCatHighRiskCommandValidator.cs:12, ClearCatHighRiskFlagCommandValidator.cs
  and TransferCatOwnershipCommandValidator.cs:13: `Reason` has `NotEmpty()` but no maximum length.
  AuditEntryAggregate.Record rejects a reason over 1000 characters, and it does so after the cat
  change has already committed (ADR-021: SaveChanges per repository call, events published after).
  - The result is a high-risk flag set with no reason audited. That breaks US-CAT-04 AC2 and SRS C-9.
  - Proven (P4/P6): `PUT .../high-risk-flag` with a 1500-character reason →
    - 500, from `DomainRuleViolationException: The audit entry is invalid` in
      AuditCatHighRiskFlaggedHandler;
    - the database shows `IsHighRisk = true`;
    - the only audit row is the interceptor's DataChange row, with no reason. The BusinessEvent row
      carrying the actor and reason is missing.
  - The same path applies to clear-flag and ownership transfer.
  - Remediation:
    - add `.MaximumLength(1000)` to `Reason` in all three validators, so the caller gets a 400
      before anything commits;
    - longer term, write the business audit row in the same SaveChanges as the change.
- [MEDIUM] Persistence/Configurations/AuditEntryConfiguration.cs:32-33 and
  `20260920110910_AddCatProfile.cs:32-33`: `before_value` and `after_value` are
  `character varying(2000)`. HO-ACC-001's AuditChangeCaptureInterceptor now snapshots the whole cat
  root row into them on every write.
  - The cat validators allow about 3,450 characters of root text: name, breed, colour, handling
    note, deceased note, and dietary and behavioural notes at 1000 each.
  - Proven (P4/P6): a medical-profile update with both notes at 1000 characters, which the validator
    permits, fails with 500 (`22001: value too long for type character varying(2000)`), and nothing
    is saved. The same update with a 700-character note → 200.
  - Two consequences:
    - legitimate clinical notes cannot be stored;
    - anyone who can write a cat (today, anyone) can fill its notes to just under the limit. After
      that, later writes that grow the snapshot fail, including setting a 500-character high-risk
      handling note. That freezes a clinical-safety flag. This is derived from the proven
      mechanism, not separately probed.
  - Remediation (integrator migration): make `before_value` and `after_value` `text`, or bound and
    truncate the snapshot deterministically in the interceptor. Add a test that saves a cat at its
    validator maxima.
- [MEDIUM] CatsController.cs:165-186, Validators/AddCatPhotoCommandValidator.cs: the photo
  `StorageKey` is fully client-controlled and trusted.
  - The server stores whatever the client says: any string up to 500 characters, a content type the
    client asserts, a size the client asserts, and a file name the client chooses. No bytes are ever
    seen, so type, size and magic bytes are unverified (NFR-SEC-5).
  - Proven (P2): customer B adds, to A's cat, the key `../../customers/<A-id>/id-card.pdf` with
    `contentType=image/png` and `fileName=<script>.html` → 201. `GET /api/cats/{id}` returns the key
    verbatim.
  - Nothing dereferences the key today (§4.2: no object storage), so there is no live path traversal
    yet. When HO-FIL-001 or any signer starts issuing URLs for stored keys, an attacker who can write
    a cat can make the server sign URLs for arbitrary objects.
    - HO-FIL-001 generates keys server-side for its own uploads.
    - Neither HO-FIL-001 nor HO-CAT-001 specifies migrating `AddCatPhoto` onto it.
  - Remediation: replace `StorageKey`, `ContentType`, `SizeInBytes` and `FileName` with a HO-FIL-001
    `fileId` that the server verifies belongs to the caller and to purpose `cat-photo`. Until then:
    - reject keys that do not match a server-issued pattern (no `..`, no leading `/`, no scheme);
    - never pass a stored key to a signer without that ownership check.
- [LOW] Infrastructure/Documents/QuestPdfCatMedicalHistoryRenderer.cs:31: the PDF fails for
  non-Latin text.
  - Proven (P5/P6): a cat named `Mishti ঝুমুর` → 500 on `medical-history.pdf`. QuestPDF throws
    `DocumentDrawingException`: glyphs are not available in the registered font 'Lato'.
  - For a Bangladesh clinic this is a denial of US-CAT-10 for ordinary customers. The same applies to
    any Bengali breed, note, vaccine or product name.
  - SafeExceptionHandlerMiddleware logs the exception message, which contains the free-text value.
    That is minor personal-data leakage into logs.
  - Remediation: register a Bengali-capable font (for example Noto Sans Bengali) as a fallback, and
    add an I20 variant with a Bengali name.
- [LOW] Read/QueryHandlers/Cat/*: clinical-record reads are not access-logged.
  - Profile, weights, vaccination status, the medical-history PDF and the cage card write no audit
    row. The only record is an Information log with the cat id and no actor.
  - This breaks NFR-SEC-8: "every access to a clinical record shall be logged".
  - Remediation: record a read-access audit entry with the actor when authorization lands. It is
    meaningless before then, because the actor is `Guid.Empty`.

CHECKED, NO FINDING
- CSRF: sound, but not enough on its own.
  - A signed-in customer's `PUT .../profile` without `X-CSRF-Token` → 403 `csrf_token_invalid`.
  - The identical request with no cookies → 200. That is not a CSRF flaw: it is the BLOCKER. Once
    anonymous callers get 401, the existing CSRF check covers the cat endpoints with no change.
- Content-Disposition and PDF text injection: none.
  - Tried names containing CRLF + `Set-Cookie`, `"; filename=evil.exe`, and `../../etc/passwd`. All
    → 200, with the name safely encoded in `filename`/`filename*` and no header injected.
  - QuestPDF `.Text()` renders plain text; there is no markup or hyperlink sink.
- QR payload: only `/cats/{catId}`. It is relative, because `CatProfile:PublicWebBaseUrl` is unset,
  and it uses a server Guid, so there is no personal data. Q7 selects the owner id and handling note,
  but neither is emitted.
- SQL injection: none.
  - The cat and vaccine code uses no raw SQL: all LINQ, parameterised by EF.
  - `AddCatProfile` has no `migrationBuilder.Sql`.
- Enum and date validation:
  - Enums are exact allow-lists by DisplayName, and an unknown value → 400.
  - Date of birth, administered-on, recorded-on and deceased-on are rejected if in the future.
  - `IntervalDays` must be 1–730, weight has min and max bounds, the photo content type is an exact
    allow-list, and the size must be 1 byte to 5 MiB.
  - There is no count cap on medical-profile lists; the audit column limit above incidentally caps
    them.
- Other mass assignment: none. Controllers map request DTOs to commands field by field, and no
  domain entity is model-bound. The only issues are the identity fields above.
- Logs: cat handlers log only ids, the vaccine code and filter flags. No names, notes or owner
  details.
- Audit coverage: every cat and vaccine write gets a DataChange row, in the same transaction, from
  the interceptor. Flag, clear and transfer also get a BusinessEvent row with reason, except in the
  overflow case above. Attribution is worthless while anonymous callers are admitted.

CONDITIONS RECORDED, NOT FINDINGS
- HO-CAT-001 §20.3 told every gate to state that US-CAT-08 AC2 is "only a query filter". Stated, and
  now measured: after HO-ACC-001 it is not even that, beyond the single `?ownerId=` list. AC2 (A18)
  cannot be certified by ACCEPTANCE until the owner-scope check exists. This closes, as FAILED, the
  condition HO-CAT-002-ACCEPTANCE carried here.
- QuestPDF Community licence (§20.6): still unconfirmed. This is not a security finding.

SCOPE CHECK
- files in allowed scope: 169 `src/`/`tests/` files in 4007225, plus the 5 C10 source and test files.
  All are HO-CAT-001 §13 paths except the host-foundation files §3.2 also lists.
- files outside allowed scope:
  - `.ai/decisions/adr.md` and `.claude/agents/handover-writer.md`;
  - 26 `docs/handover/HO-US-ACC-*` files in 4007225.
  - None of them adds security surface.
