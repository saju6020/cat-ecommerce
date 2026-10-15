GATE: SECURITY
HANDOVER: HO-PHM-002
COMMITS: a8761829..aa78e655 (branch ho/PHM-002, head aa78e655)
DATE: 2026-10-11
STATUS: PASS

FINDINGS
- [LOW] web/lib/api/pharmacy.ts, prescriptions.ts, my-prescriptions.ts, controlled-drugs.ts, diet-authorisations.ts, formulary.ts — route-param ids (`prescriptionId`, `dispensingId`, `entryId`, portal `id`) are interpolated into API paths without `encodeURIComponent`, unlike most other `lib/api` modules. If a decoded param holds `../` or `?`, the fetch resolves to a different same-origin endpoint (client-side path traversal). Not exploitable as found: automatic calls are GETs whose responses the attacker never sees, every POST needs a deliberate click on a screen that would not render from a mismatched body, and the server's own authorization and CSRF checks still apply. Defence in depth: encode path segments. Backlog PHM row 42.

Checked and clean:
- Witness credentials (ADR-FE-063, U-C8): held only in `WitnessDialog` local state; password cleared on submit (before `onSubmit`), on close (`open=false`), on unmount and on every refusal (busy->idle with an error, and on any new error), focus returns to the identifier. Both fields `autocomplete="off"` plus `data-1p-ignore`/`data-lpignore`; the form is `autoComplete="off"`. Dispense, reverse and register correction are plain `useCallback` functions (`useDispense`, `useReverseDispensing`, `useRecordCorrection`), not `useMutation`, so no `variables` sit in the mutation cache. Credentials never appear in a query key, URL, storage or log; parent screens keep only `witnessOpen`/`witnessError`. `ApiValidationError` keeps the response body, not the request. No error reporter (Sentry etc.) is wired up that could capture request bodies.
- CSRF / X-Branch-Id: all writes go through `apiFetch` (CSRF on unsafe methods, branch on every request). The register CSV uses the branch-aware `ExportButton`, not an anchor.
- XSS: no `dangerouslySetInnerHTML`/`innerHTML` in the slice. The label's `<style>` is a static string built from two constants. Every label field, instruction, note and reason is rendered as a React text node.
- Portal: only `/api/customers/me/**`. The `MyPrescription*` types carry no batch/lot, safety, cost or staff-only fields. Ownership is enforced by the server (404 copy names no other owner). `?action=repeat` is only compared to a literal and opens a dialog. `bookLink` (absolute) is ignored in favour of a relative `/book?catId=`.
- No open redirects: every `router.push`/`replace` and `href` is a relative path built in the app; the register `sourceHref` maps only known source types to `/staff/...`.
- Role gating: `useStaffRoles` is presentation only. Actions come from the server's `allowedActions`/`canDispense`/`canRequest`, and refusals render the server's 403.
- Errors: codes are mapped to client sentences; 5xx/unknown give the generic message; no raw server internals are shown.
- No `console.*` added. The passwords in `mocks/` and `tests/e2e` are MSW fixture values (the admin one already existed in `mocks/handlers/areas/accounts.ts`) and are not real secrets; the mock worker is env-gated in `app/providers.tsx`.

SCOPE CHECK
- files in allowed scope: 117/117 (web/**, docs/handover/HO-PHM-002.md, docs/test-cases/TC-HO-PHM-002.md)
- files outside allowed scope: none
