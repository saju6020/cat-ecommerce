GATE: SECURITY
HANDOVER: HO-EMR-002
COMMITS: 57666ac4 a00a9ab2 9245fea0 d69a71f7 d65fff82 4b6f505a 53696ad5 (base d3527166)
DATE: 2026-10-08
STATUS: PASS

FINDINGS
- [LOW] web/app/(staff)/staff/diagnostics/[orderId]/page.tsx:36 — result entry/attach on the standalone order page is gated by `roles.includes('Vet')` rather than a server `allowedActions` value (ADR-FE-051), so a Vet sees result entry after finalise with no advertised action. Not exploitable: `StaffDiagnosticsController` RecordResults/AttachFile/Cancel carry `[RequireAction(Actions.Visits.WriteNote)]` (Vet only, ActionRoles.Consultation.cs:13) and every handler refuses `order.BranchId != command.BranchId` (DiagnosticHandlers.cs:109/177/212); results after finalise are allowed by the spec (FinaliseDialog copy, US-CLN-14). Backlog: have the order view model carry its own allowedActions so the UI does not re-derive role rules.
- [LOW] web/components/visit/DischargeSummaryView.tsx:61 (+ lib/api/visits.ts getDischargeSummaryPdfUrl) — the staff "Download PDF" is a plain `<a href download>`, so it carries no `X-Branch-Id`. Fails closed, not open: `HttpContextActiveBranchAccessor` falls back to the caller's PRIMARY branch and the query is branch-scoped, so a multi-branch member working a non-primary branch gets a 404 instead of the PDF. Functional gap, no data exposure. Backlog: fetch the PDF through apiFetch as a blob, or pass the branch another way the accessor validates.
- [LOW] src/Application/.../Visit/ConsentHandlers.cs:83 (HO-EMR-001 backend, outside this FE handover) — `WitnessStaffId` from the consent request is stored without checking it is a staff account at the branch; the FE picker only offers on-shift performers, but a crafted request can name any GUID as witness. Integrity of the consent record, not disclosure. Backlog for EMR-001: validate the witness against staff branch assignment.

Checked and clean
- Browser storage (U-A9): no localStorage/sessionStorage/IndexedDB/Cache API writes in shipped code; autosave (useConsultationAutosave.ts) goes only to `PUT …/draft` via apiFetch (CSRF + X-Branch-Id via client.ts), failures never replayed. The only sessionStorage use is mocks/handlers/visits/store.ts, loaded only when NEXT_PUBLIC_USE_MOCKS=true through providers.tsx's dynamic import. Clinical text never appears in URLs or query strings (router.push/href carry ids only).
- Rendering (U-X2): no dangerouslySetInnerHTML/innerHTML in the diff; ClinicalText is React text with pre-wrap; consent body only via the existing LimitedMarkup; the signature is an `<img>` of the server's SVG, so no inline SVG script context.
- Signed URLs (U-X4): only inside payloads, never in query keys, logs or storage; queries holding them have staleTime 4 min (URLs live 5); FileThumb refreshes once on img error; new-tab links use noopener noreferrer; DICOM is download-only.
- Portal: only `/api/customers/me/...` endpoints, under the separate `['my', …]` key namespace; the server enforces `Visits.ViewOwn` with OwnerScope.Cat/CatCollection and the finalised-only filter (CustomerVisitsController); a 404 gives the neutral message.
- Actions: workspace controls come from `allowedActions`; every mutating endpoint is server-gated by RequireAction plus a branch match.
- Uploads: FileUploadField wraps HO-FIL-002's FileUploader with purpose LabResult/ClinicalPhoto/ConsentForm; the server checks purpose access (FilePurposeAccess.cs) and attach eligibility (IsEligibleForAttach, one file per consent).
- Signature pad: strokes are held in a ref and sent as normalised points; there is no toDataURL, storage or logging.
- No new dependencies (no package.json/lock change) and no console logging in shipped code.

SCOPE CHECK
- files in allowed scope: 133/133 (web/**, docs/test-cases/TC-HO-EMR-002.md, web/docs/decisions.md)
- files outside allowed scope: none
