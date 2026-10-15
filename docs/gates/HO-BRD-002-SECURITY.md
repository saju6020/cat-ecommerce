GATE: SECURITY
HANDOVER: HO-BRD-002
COMMITS: ef4bba16 0e24d681 baf7f254 111c8cc2 bb1d3862 baf68c86 df3720a6 70b7936b 669744e6 c65bf03a 05233fd7 5d579280 8b4c8073 75cb8e85 (range 5a187b6b..75cb8e85)
DATE: 2026-10-15
STATUS: PASS

FINDINGS
- [LOW] web/components/boarding/stay/CheckOutWizard.tsx:64-71,104-126 — signature data stays in memory after submit. After a successful check-out, the `drafts` state (typed signer name and drawn strokes) stays mounted until staff press "Done", because the result panel renders inside the same `Wizard`. In both wizards the TanStack `useMutation` also keeps `variables`, which holds the full `ownerSignature`, in the in-memory MutationCache for the default gcTime (5 min) after unmount. Nothing is persisted: no query persister, no localStorage, no service-worker cache of /api, no console logging. So exposure needs the same browser session and devtools. Remediation: call `setDrafts` with EMPTY_SIGNATURE for every cat right after `mutateAsync` resolves. Call `checkIn.reset()` / `checkOut.reset()` (or set `gcTime: 0` on these two mutations) once the result is shown.
- [LOW] web/tests/e2e/boarding-real-api.spec.ts:41 — `BRD_SEED` defaults to `seed.json` in the cwd (`web/`). That file holds the admin and customer passwords. There is no `.gitignore` entry for it, so a hand-made seed file is easy to `git add -A` by mistake. No seed file is committed in this range. The only literal password, `Password123!` in boarding-booking.spec.ts, is an MSW fixture. Remediation: make `BRD_SEED` required (fail fast when it is unset) or point the default at the OS temp dir. Add `web/seed*.json` to `.gitignore`.
- [LOW] web/components/boarding/daycare/DaycareBookingForm.tsx:72-76 — daycare does not use the link rewrite. The `vaccinationGate` taken from the daycare 422 payload is not passed through `toInternalLink`, unlike `normaliseEnquiry`. When the API sends an absolute `bookingLink`, `isSafeInternalPath` rejects it and the "Book a vaccination" link is hidden. This fails closed, so it is not exploitable. It is listed for consistency only: apply `toInternalLink` to `blockedCats[].bookingLink` here too.

Reviewed, no exploitable issue:
- Owner pages: all /portal/boarding/** and /boarding/** components call only `lib/api/my-boarding.ts` (BASE `/api/customers/me/boarding`, `/api/customers/me/daycare`). No staff module is imported on the customer side. Severity filtering and ownership are left to the server. The UI does not hide or filter anything that matters for security.
- Gate link rewrite: `toInternalLink` (my-boarding.ts:237) keeps only `pathname + search` of an absolute URL. The result still has to pass `isSafeInternalPath`, which refuses `//`, `/\`, backslashes and control characters. Tested inputs: `javascript:` gives "alert(1)" (refused); `https://evil.example//x` gives "//x" (refused). Another host is never linked.
- Sign-in `?next=`: the `/boarding` entry uses the existing `sanitizeNextPath` and `matchesPrefix`. That allows an exact match or `/boarding/…` only, checks the origin and refuses `//`. No open redirect.
- XSS: there is no `dangerouslySetInnerHTML`, `innerHTML` or `eval` in the slice. Incident notes, diary text, observations, care profiles and enquiry messages render as React text. `tel:` links use server phone numbers on staff screens only. The diary `fileUrl` comes from the FIL-001 signed-file view model (`file.url`), opens with `rel="noreferrer"`, and React 19 blocks `javascript:` hrefs.
- Path ids: every interpolated id in my-boarding.ts, staff-boarding.ts, boarding-care.ts, boarding-public.ts and boarding-links.ts goes through `encodeURIComponent`. Queries use `URLSearchParams`.
- Uploads: `BoardingBelongings` and `BoardingMedia` go through FIL-002's `FileUploader` with `audience="staff"`. Only `fileId` is sent. Owners see diary photos through signed URLs.
- Money: the deposit panel only calls confirm, with no amount. "Record payment" links to `/staff/billing/payments/new?customerId=<encoded>`. The amounts that are sent are staff inputs (rate cards, vet-approval request), not payments.
- Admin override: the control is shown on blocked cats, requires a reason, and the server decides.
- Secrets: no connection strings, tokens or real credentials in the range. The real-API spec reads everything from env or the seed file, and it is skipped unless `BRD_REAL_API=1`.

SCOPE CHECK
- files in allowed scope: 122/122 (web/** for Boarding FE, the sign-in allow-list line, mocks/tests, docs/handover/HO-BRD-002.md, docs/test-cases/TC-HO-BRD-002.md)
- files outside allowed scope: none

## Proposed rows for `docs/backlog/BRD.md` (numbered from 67; status after the 2026-10-15 fix round)

| # | Sev | Status | Where | Item |
|---|---|---|---|---|
| 67 | LOW | Fixed | `CheckOutWizard.tsx`, `CheckInWizard.tsx`, `useStaffBoarding.ts` | Signature drafts are cleared after a successful submit; both mutations use `gcTime: 0` and are reset. |
| 68 | LOW | Fixed | `boarding-real-api.spec.ts`, `.gitignore` | `BRD_SEED` is required; `web/seed*.json` is git-ignored. |
| 69 | LOW | Fixed | `DaycareBookingForm.tsx`, `BookingFlow.tsx`, `my-boarding.ts` | The daycare and reserve gate payloads go through `normaliseGate` / `toInternalLink`. |
