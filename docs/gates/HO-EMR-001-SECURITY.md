GATE: SECURITY
HANDOVER: HO-EMR-001
COMMITS: b9c80c5 47612a9 baab800 9d9e2d4 d786ba4 9696dbf 5e4cf63 0805bf1 650556a e1c5e10 3b39c31 75d8c46 (base master 50f1ab1; c088ecf is the parallel CODE_REVIEW verdict, docs only)
DATE: 2026-10-07
STATUS: PASS

No BLOCKER or HIGH finding. Phase F (treatments, dispensary) is deferred by owner decision and was not reviewed.

Verified (no finding):
- Every new endpoint has [RequireAction] with the §12.3 role mapping. Visits.Finalise is Vet only, enforced by
  the filter (403 plus audit) and again in VisitAggregate.Finalise using roles read from the caller's own
  staff account. Visits.ViewOwn has no staff role, so a staff caller on a portal route gets 403.
- Branch scoping: X-Branch-Id is checked against the staff assignment (HttpContextActiveBranchAccessor).
  Every visit, order and consent command and every by-id read checks BranchId, and a mismatch returns 404
  (ClinicalResponses.NotFoundCodes). Clinical history is cross-branch by design (§8.7).
- Portal: the cat routes use OwnerScope.Cat. The visit-by-id routes use CatCollection (always allowed), but
  the query handler checks finalised status and the current owner (c.OwnerId == caller), so a non-owner,
  a previous owner or an open visit gets 404. Lab results cover finalised visits only, omit DICOM and
  show no staff names.
- Access log: [ClinicalRecordAccess] is on every clinical read. The action filter runs after authorization,
  fails closed (500, action not run) and sets Cache-Control: no-store. PDF and SVG responses use
  private, no-store and nosniff. The SVG also gets CSP default-src 'none'; sandbox and is built only from
  numbers.
- File attach: IsEligibleForAttach requires matching purpose, uploader == caller and the 30-minute window.
  A cross-table "already attached" pre-check runs, and unique indexes are the backstop.
  FilePurposeAccess maps LabResult and ClinicalPhoto to Visits.WriteNote (Vet) and ConsentForm to
  Consents.Record. Customers still get CatProfilePhoto only, and unmapped purposes are still refused.
- Triggers: no string-built SQL from input. The status value comes from the enumeration. CR001 is mapped
  to a logged 409. The consent evidence columns are frozen. TRUNCATE is refused.
- Audit and erasure: business audit rows hold only ids and enums. ConsultationDraftAggregate is the only
  addition to the capture exclusions. The consultation erasure exemption is justified: no customer id on
  visit rows, and the consent signer name is a Regulatory record. Notifications are link-only.
- Validators: every free-text field has a length limit and NoControlCharacters. Signature strokes are
  bounded.
- Test guard: ConsultationApiFactory throws unless CATECOMMERCE_TEST_DB is set and does not name
  catecommerce_dev. Every EMR endpoint test class uses this fixture, so the direct-SQL fallback in
  ConsultationIntegrationTestBase is never reached.

FINDINGS
- [MEDIUM] src/Infrastructure/CatEcommerce.Infrastructure/Files/StoredFileEraser.cs:32 with migrations AddDiagnostics/AddConsents (clinic.stored_file_referenced) — The delete guard does not protect clinical file bytes. The eraser deletes the GCS object first and the row second, so the BEFORE DELETE trigger only fires after the object is already gone. FileStorageModule registers a StoredFilePurgeSource for every FilePurpose, including LabResult, ClinicalPhoto and ConsentForm. IsEligibleForPurge does not exclude referenced files, and attaching a file sets neither RetainUntil nor LegalHold. This is latent today: nothing consumes IRetentionPurgeSource and DeleteStoredFileCommand has no endpoint. Once a retention worker is wired, attached lab reports, photos and signed consent forms lose their content and the row stays, pointing at nothing. Fix: add a reference check (visit_photo, diagnostic_attachment, consent_record) to the eligibility predicate, or in StoredFileEraser before the object is deleted; or set RetainUntil/LegalHold when a file is attached.
- [MEDIUM] src/Infrastructure/CatEcommerce.Infrastructure/Persistence/Migrations/20261007080435_AddClinicalRecordImmutability.cs (clinic.visit_child_frozen) — On UPDATE the function checks only NEW.visit_id. A statement such as UPDATE clinic.visit_vitals SET visit_id = '<open visit>' WHERE visit_id = '<finalised visit>' moves a frozen row onto an open visit, where it can then be edited. This bypasses the "psql prompt" layer the migration documents. There is no app path. Fix: on UPDATE, also refuse when OLD.visit_id's visit is finalised, or refuse any change of visit_id.
- [LOW] AddDiagnostics/AddConsents/AddVisits triggers — The is_marked_to_delete soft-delete column is not guarded. It is absent from the consent_record_guard column list, diagnostic_order has no UPDATE trigger at all (cat_id, visit_id and branch_id can be rewritten), and an open visit can also be marked. Setting the flag hides the record from every read, which in effect deletes it. There is no app path. Fix: freeze is_marked_to_delete and the identity columns in these triggers.
- [LOW] src/Api/CatEcommerce.Api/Filters/ClinicalRecordAccessFilter.cs:42 — For routes that carry only visitId, orderId or consentId, the access-log row has no CatId. §8.11 says the cat id is "resolved by the record id". Answering "who read cat X's record" therefore needs a join through the clinical tables. Fix: resolve the cat id in RecordClinicalRecordAccessCommandHandler.
- [LOW] src/Application/CatEcommerce.Application/CommandHandlers/Visit/RecordClinicalRecordAccessCommandHandler.cs:41 — AfterValue stores "Path=<decoded request path>;CatId=..." without escaping. The {section} segment of the clinical-history route is free text that is logged before it is validated, so a staff caller can insert ";CatId=<other>" tokens into the audit value (EntityId stays correct). A path longer than 500 characters fails the validator and returns 500. Fix: log the route template plus the parsed ids rather than the raw path, or encode the value.
- [LOW] src/Api/CatEcommerce.Api/Controllers/StaffConsentsController.cs:63,95 — GET /api/staff/cats/{catId}/consents and GET /api/staff/consents/status are not branch-scoped, while GET /consents/{id} is. Reception at any branch can list any cat's consents (kind, including euthanasia, signer name and status) by cat id. The status endpoint is also not access-logged and has no no-store. Fix: if Consent is meant to be branch-scoped, filter the list by the active branch; otherwise record that it is deliberately cross-branch like clinical history, and add [ClinicalRecordAccess] or no-store to status.

SCOPE CHECK
- files in allowed scope: 192/192 for security purposes. The cross-area edits (Clinic Cancel/Complete
  handlers, AuditCaptureExclusions, UniqueConstraintExceptionFilter, FilePurposeAccess,
  AppointmentsApiFactory/FilesEndpointTests test adjustments) are each the narrow edits §13 and HO-FIL-001
  OQ-7 name. Detailed scope accounting belongs to the ARCHITECTURE gate.
- files outside allowed scope: none found by this gate
