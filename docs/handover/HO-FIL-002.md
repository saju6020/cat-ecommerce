# HO-FIL-002 — File Storage (frontend)

## 1. Identity

| Field | Value |
|---|---|
| Handover ID | **HO-FIL-002** |
| Title | Shared uploader, file list and media viewer — frontend slice |
| Date | 2026-09-23 |
| Author | Claude Opus 5 |
| Backlog | [`docs/USER_STORIES.md`](../USER_STORIES.md) v2.4 §17 — **US-XCT-07**, applied through every area that uploads |
| SRS | [`docs/SRS.md`](../SRS.md) v2.3 — NFR-SEC-5, **UI-1 … UI-8**, AC-18 (accessibility), NFR-PRF-4 |
| Pairs with | [`HO-FIL-001`](HO-FIL-001.md) — **its §12.4 is this slice's contract** |
| Builds on | [`HO-ACC-002`](HO-ACC-002.md) (app shell, fetch wrapper, error surface), [`HO-ADM-002`](HO-ADM-002.md) (staff layout patterns) |
| Consumed by | `HO-SHP-002`, `HO-EMR-002`, `HO-BRD-002`, `HO-RPT-002` — they compose these components; none writes its own uploader |
| Delivery order | Section 6 of 18 in [`README.md`](README.md#delivery-order) |
| Baseline commit | `529ff4f` |
| Amended | **2026-09-27 — implementation.** (1) §11's `web/components/files/useFileUpload.ts` is built at **`web/lib/hooks/useFileUpload.ts`** instead. `web/README.md`'s scaffold convention (carried from HO-CAT-002 §8.2 rule 3, enforced by an ESLint `no-restricted-imports` rule for `useQuery`/`useMutation`) states hooks that own request/async state live in `lib/hooks/`, not `components/`; `useFileUpload` owns exactly that kind of state for this slice's one other network call (ADR-FE-150), so it follows the same rule rather than being a one-off exception. `components/files/` still owns everything else §11 names. (2) §8.3's "a button wrapping a visually hidden input" (ADR-FE-152) is built with the input as a **sibling** of the button under one wrapping `<div>`, not a DOM descendant — nesting it caused the input's own programmatic `.click()` to bubble a real click event back through the button's own handler, double-invoking it (reproduced directly, see `web/docs/decisions.md`'s ADR-FE-152 entry for the exact mechanism). Every observable behaviour §8.3 asks for (keyboard-operable, screen-reader-operable, drag-and-drop layered on top, no pointer required) is unchanged. (3) One outstanding gap, not built: ADR-FE-154's refresh-once behaviour is wired for `<img>`/`<video>` (both fire a real `onerror`) but not for the `<iframe>`-embedded PDF path — an expired PDF signed URL has no load-failure signal this slice can observe from an `<iframe>`, so it silently renders whatever body the `403` sends. No consuming area exercises a PDF through `<MediaViewer>` yet, so this has not blocked anything; flagged for whichever consuming handover is first to pass a PDF through it. |
| Amended | **2026-09-27 — implementation, outside §13.** Files touched that are not literally under `web/components/files/` or `web/mocks/handlers/files/`, each with its reason: (1) **`web/lib/api/files.ts`** — `uploadFile()`, the `XMLHttpRequest`-based exception this handover's own §7/§11 calls for (ADR-FE-150). Kept in `lib/api/` rather than `components/files/` or `lib/hooks/`, mirroring how every other area keeps its endpoint functions in `lib/api/<area>.ts` (`web/README.md`) — this is that pattern's one non-`fetch` instance, not a second HTTP client (HO-FIL-002 §14 forbids a second client/auth path; this is the same client's one documented exception, not a second one). (2) **`web/lib/hooks/useFileUpload.ts`** — see the row above. (3) **`web/mocks/handlers/areas/file-storage.ts`** — this file already existed (an empty `fileStorageHandlers: HttpHandler[] = []` stub) as the scaffold's own one-file-per-area wiring point, already imported by `mocks/handlers/index.ts` (`web/README.md`: "populate your own `mocks/handlers/areas/<area>.ts`"); it now re-exports the real handlers built under `web/mocks/handlers/files/` (§12's own named path), reconciling the handover's literal path with the scaffold's actual wiring convention — the same class of mismatch HO-FIL-001's own backlog records against its §13 (`docs/backlog/FIL.md` #7). No other area's file was touched. |
| Amended | **2026-09-27 — implementation, test-only harness (outside §13/§14, self-authorised, flagged for review).** HO-FIL-002 §14 forbids "any page, route or layout" — but this slice's own required gates (axe and overflow e2e coverage, per the task brief commissioning this build) cannot be met without a real browser rendering these components on an actual route, and no consuming handover (HO-SHP-002/HO-EMR-002/HO-BRD-002/HO-RPT-002) exists yet to host them. `web/app/dev/file-storage-harness/page.tsx` was added as a minimal, unlinked (no `nav/` entry), clearly-commented test harness mounting `<FileUploader>`/`<FileList>`/`<MediaViewer>` together, solely so `tests/e2e/files-a11y.spec.ts`/`files-overflow.spec.ts`/`files-upload.spec.ts` have something real to drive. This was not owner-authorised — it is this implementer's own judgement call, made explicitly rather than silently, that the alternative (skip e2e/axe coverage for this slice entirely) was worse. Flagged here for the owner/integrator to confirm, reject, or replace once a real consuming page exists — at that point this harness route should likely be deleted and the specs re-pointed at the real page. |
| Amended | **2026-09-27 — cat-photo page conversion, not done here (task brief's own branch condition).** HO-CAT-002 is not listed in this handover's §1 "Consumed by" row (only HO-SHP-002/HO-EMR-002/HO-BRD-002/HO-RPT-002 are), and the Cat Profiles photo page falls under no path in §13's Allowed scope — converting it is squarely "any page, route or layout" (§14, forbidden). Per the task's own instruction for this exact case, the uploader was built without touching the cat-photo page; see the task's final report for exactly what that conversion still needs. |
| Amended | **2026-09-28 — owner decision on HO-FIL-002-ARCHITECTURE's BLOCKER (first pass) and HO-FIL-002-SECURITY's MEDIUM 1, confirmed 2026-09-28.** The 2026-09-27 "test-only harness" row above asked the owner to choose: authorise `web/app/dev/file-storage-harness/page.tsx` with the route made unreachable outside development, or delete it. **The owner authorises it, kept as a development-only test page, to be deleted once a real consuming screen exists** (HO-SHP-002, HO-EMR-002, HO-BRD-002 or HO-RPT-002, whichever lands first — §3's own consumer list). Made unreachable in a production build: `page.tsx` now calls `notFound()` (`next/navigation`) **during render**, unconditionally, the instant `process.env.NODE_ENV === 'production'` — not from an effect, so a production request never renders anything else at all, server-side or client-side. The actual content (a live `<FileUploader>` wired to the real `POST /api/staff/files`, plus the mock gate/fixtures modules) was split into `web/app/dev/file-storage-harness/HarnessContent.tsx`, loaded only through `React.lazy`'s dynamic `import()` — since the branch that renders it is never reached in production, that `import()` is never issued and the module's own chunk is never fetched by a production client. `tests/unit/files/file-storage-harness-page.test.tsx` proves both halves: `notFound()` fires and nothing renders when `NODE_ENV` is `production`; the real content still mounts, and `notFound()` is never called, otherwise. |
| Amended | **2026-09-28 — gate-fix round (ARCHITECTURE MEDIUM 2/3/4, ARCHITECTURE LOW 1, SECURITY MEDIUM 2, SECURITY LOW 1/2).** Six further fixes, each keeping this handover's own contract unchanged: (1) **`fileRules.ts`/`fileErrors.ts` moved from `web/components/files/` to `web/lib/files/`** (ARCHITECTURE MEDIUM) — `lib/hooks/useFileUpload.ts` importing them at their §11 location made it the one `lib/` → `components/` import in the app; moving the two modules instead of moving the hook back keeps `useFileUpload` at its own already-recorded location (the row above) while fixing the dependency direction. `components/files/*` still imports both from their new location — that direction is unaffected. (2) **The ADR-FE-154 refresh-once guard (`<FileThumb>`/`<MediaViewer>`) is now keyed on `fileId` alone, never reset by a `url`-only prop change** (SECURITY MEDIUM) — it used to reset whenever `file.url` changed, so a host that re-fetches its own record and passes a new, still-bad URL for the same file (exactly what ADR-FE-154 asks hosts to do) refreshed forever. (3) **The `urlExpiresOn` half of §8.4's trigger is now built**, not only "on an image error" (ARCHITECTURE MEDIUM) — both components proactively check `urlExpiresOn` in an effect and call `onRefresh` before ever asking the browser to load a URL already known to be dead, for images and video (the PDF `<iframe>` gap recorded above is unchanged). (4) **`lib/api/files.ts`'s `uploadFile` now routes a `401` through `lib/api/client.ts`'s session-error handler** (ARCHITECTURE MEDIUM) — a new, minimal export, `notifySessionError(body)`, forwards to the same module-private `sessionErrorHandler` `apiFetch` already uses, so a session that expires mid-upload reaches the HO-ACC-002 idle-lock/sign-in surface instead of only ever reading as "did not finish uploading". A `403 csrf_token_invalid` now rejects with a new `FileUploadForbiddenError`, distinct from both a validation error and a network failure. (5) **An unrecognised server `errorCode` is never shown verbatim** (SECURITY LOW) — `KNOWN_VIOLATION_CODES` allow-lists the real HO-FIL-001 §10.2 codes; anything else becomes `FileUploadValidationError`'s new `'UnknownViolation'` code, mapped by `fileErrors.ts` to a generic sentence. (6) **A `413`/`502` keeps its own violation code** (ARCHITECTURE LOW) — `FilesController` answers both with the same `ValidationResponse` shape as a `400`; `uploadFile` now parses that shape for any status it doesn't otherwise special-case, instead of only for `400`/`403`. Also, in the same round: `isRenderableImageContentType()` (`lib/files/fileRules.ts`) excludes `image/svg+xml` explicitly rather than relying only on the upload allow-list (ARCHITECTURE MEDIUM, ADR-FE-158); `referrerPolicy="no-referrer"` was added to the `<iframe>`, `<img>` and download `<a>` in `<MediaViewer>`/`<FileThumb>`/`<FileList>` (`<video>` cannot carry it — not a defined attribute for that element) as defence in depth (SECURITY LOW), with no `sandbox` attribute added to the PDF `<iframe>` (Chromium refuses to render a PDF inside a sandboxed frame); and `web/package-lock.json`'s unrelated `devOptional`→`dev` churn (ARCHITECTURE LOW) was reverted, restoring it to the baseline the branch started from. `web/docs/decisions.md`'s ADR-FE-152/154/157/158 entries and every affected module's own doc comment were updated to match. New/extended tests: `tests/components/files/file-thumb.test.tsx` and `media-viewer.test.tsx` (a host-driven prop update to a new, still-failing URL for the same `fileId` does not spend a second refresh), `tests/unit/files/files-api-error-routing.test.ts` (401 session routing, CSRF 403, an unrecognised code, 413/502 violation-code preservation). **A further file outside §13**, beyond the three already recorded above: **`web/lib/api/client.ts`** gains the one `notifySessionError` export named in item (4) — an additive, four-line change (a function plus its own call site swapped from the private variable it already read); no new HTTP client or auth path is introduced (§14 still holds), and nothing about `apiFetch`'s own existing behaviour changes. |
| Amended | **2026-09-28 — owner decisions on HO-FIL-002-ACCEPTANCE (first pass, BLOCKED on §18 A1/A6), confirmed 2026-09-28.** (1) **A1 reworded.** The evidence "the four consuming handovers import it" is contradicted by the consumers as written: HO-SHP-002 (ADR-FE-035, §20.7) and HO-BRD-002 (ADR-FE-104, §20.7) forbid any upload until storage is provisioned, HO-RPT-002 has none, and HO-EMR-002 §8.2 plans its own multipart upload in `web/lib/api/files.ts`. A1 now reads *"One uploader exists; each consuming handover's own ACCEPTANCE verifies that it adopts it."* SHP/BRD stay upload-free. **Owed, not done here:** (a) HO-EMR-002 §8.2 is to be amended to use `useFileUpload`/`<FileUploader>` and the existing `lib/api/files.ts` instead of building a second upload path; (b) the cat photo page's `components/cat/PhotoField.tsx` is to be converted to `<FileUploader>` (backlog FIL #12). (2) **A6 deferred to the Phase 6 performance pass.** Owner: the project owner. Trigger: the first screen that renders a list of `<FileThumb>` images (for example the boarding diary, once storage exists). HO-FIL-001 OQ-5 still owns the fix; backlog FIL #21 points here. |

---

## 2. Objective

Build the **one** upload control and the **one** media viewer this product has, so that a shop
manager adding a product photo, a vet attaching a lab PDF and a caretaker uploading a diary photo all
meet the same component — with the same progress, the same error wording, the same keyboard behaviour
and the same screen-reader announcements.

Four frontend handovers were each about to build their own. This slice is what they import instead.
It is deliberately unopinionated about layout: it renders into whatever page hosts it.

---

## 3. In scope

| Component | Purpose | Used by |
|---|---|---|
| `<FileUploader>` | Choose or drop files, validate client-side, upload with progress, retry, remove | all four |
| `<FileList>` | Uploaded files with name, size, type icon, download/remove | EMR, BRD, RPT |
| `<MediaViewer>` | Accessible dialog for images and video; page-by-page for PDF via the browser viewer | EMR, BRD, POR |
| `<FileThumb>` | Small preview with a type-appropriate fallback (no broken-image icon) | SHP, BRD |
| `useFileUpload()` | The hook behind the uploader: queue, abort, progress, error mapping | all four |

---

## 4. Out of scope

| Not here | Owner |
|---|---|
| Where the file attaches (a visit, a product, a stay) | The consuming page passes `purpose` and its own submit |
| Any layout, page or route | Consuming handovers |
| Image cropping, rotation, editing | Nobody — HO-FIL-001 OQ-4/OQ-5 |
| A gallery or lightbox with navigation across records | HO-BRD-002 owns the diary's grouping; this slice opens one item |

---

## 5. Prerequisites

| Prerequisite | State |
|---|---|
| HO-FIL-001 §12.4 (upload contract, view model, violation codes) | ✅ specified in this pair |
| HO-ACC-002 app shell, fetch wrapper, toast/error surface | ✅ written |
| MSW set up (HO-ACC-002) | ✅ written |

---

## 6. Governing documents

| Document | What binds |
|---|---|
| `web/docs/conventions.md` | Component shape, naming, the `use` prefix for hooks |
| `web/docs/decisions.md` | ADR-FE numbering — this slice owns **ADR-FE-149 … ADR-FE-158** |
| HO-ACC-002 §7 | The fetch wrapper, error mapping and toast conventions; this slice adds no second HTTP client |
| WCAG 2.1 AA (AC-18) | 1.1.1, 1.3.1, 1.4.3, 2.1.1, 2.4.3, 4.1.2, 4.1.3 — the live region in §8.3 is the load-bearing one |

## 7. Build rules

- **No new dependency without a line here.** This slice uses the platform `fetch` wrapper and
  `XMLHttpRequest` (for upload progress, which `fetch` does not report) — nothing else.
- Components are **controlled**: the host owns the file list state, so a failed form submit does not
  lose the uploads.
- Every user-facing string comes from the consuming page or a default in this slice; no string is
  built by concatenating a server message.

---

## 8. Frontend architecture

### 8.1 The upload flow — ADR-FE-149, ADR-FE-150

The uploader validates **before** it sends: type against the purpose's allow-list and size against
its cap, both mirrored from HO-FIL-001 §8.3 into `fileRules.ts`. A 60 MB file is refused instantly
rather than after a minute of uploading. The server re-checks everything; the client check is
courtesy, never trust (**ADR-FE-149**).

Uploads are **one request per file**, sequential, each abortable (**ADR-FE-150**). Sequential because
a vet on clinic Wi-Fi uploading eight photos gets a usable progress bar and a failure that names one
file, instead of eight parallel requests that all slow to a crawl and fail together.

### 8.2 Error mapping — ADR-FE-151

HO-FIL-001's violation codes map to sentences a person can act on. The server's message is **not**
shown raw:

| Code | Shown |
|---|---|
| `FileTooLarge` | "That file is {size}. The largest allowed here is {max}." |
| `FileTypeNotAllowed` | "{name} is a {type} file. You can upload {list}." |
| `FileContentMismatch` | "{name} does not look like a {type} file. It may be renamed or damaged." |
| `FileRejected` | "{name} was rejected by the virus scanner and has not been saved." |
| `FileScanUnavailable` | "The virus scanner is unavailable, so files cannot be uploaded right now. Please try again shortly." |
| network / abort | "{name} did not finish uploading." with **Retry** |

**`FileScanUnavailable` is not an error the person caused**, so it is phrased as a service state and
the file stays in the queue for retry (**ADR-FE-151**).

### 8.3 Accessibility — ADR-FE-152, ADR-FE-153

- The drop zone is a **`<button>` wrapping a visually hidden `<input type="file">`**, so keyboard and
  screen-reader users get the real control and everyone else gets drag-and-drop (**ADR-FE-152**).
  Drag-and-drop is an enhancement; nothing is reachable only by dragging.
- Progress is a `<progress>` element with an accessible name per file. Completion, failure and removal
  are announced once each in an `aria-live="polite"` region (**ADR-FE-153**); progress percentages are
  **not** announced, because a chatty live region is worse than no live region.
- `<MediaViewer>` is a modal dialog: focus trapped, `Esc` closes, focus returns to the thumb that
  opened it, `<img>` carries the caption the consuming area supplies as its alt text, `<video>` has
  `controls` and no autoplay.

### 8.4 Signed URLs expire — ADR-FE-154

Every URL from HO-FIL-001 lives five minutes and carries `urlExpiresOn`. A diary left open in a tab
will have dead URLs. On an image error **or** a past `urlExpiresOn`, the component re-fetches the
record's read to get fresh URLs, once, then shows "This file could not be loaded — refresh the page."
It never retries in a loop, and it never treats an expired URL as a missing file (**ADR-FE-154**).

### 8.5 Thumbnails do not exist — ADR-FE-155

HO-FIL-001 stores originals only (its OQ-5). `<FileThumb>` therefore renders the full image at
display size with `loading="lazy"`, `decoding="async"` and explicit `width`/`height` to avoid layout
shift. **This is a known cost on the boarding diary**, which may show a phone-camera photo per day;
§18 A6 records it as measured rather than assumed.

---

## 9. ADRs and open questions

| ADR | Decision |
|---|---|
| ADR-FE-149 | Client-side validation mirrors `FileRules`; the server is still the authority |
| ADR-FE-150 | One request per file, sequential, abortable, `XMLHttpRequest` for progress |
| ADR-FE-151 | Violation codes map to owner-facing sentences; `FileScanUnavailable` reads as a service state and keeps the file queued |
| ADR-FE-152 | Real `<input type="file">` behind a `<button>`; drag-and-drop is an enhancement |
| ADR-FE-153 | One polite announcement per terminal event; never per progress tick |
| ADR-FE-154 | Expired signed URLs trigger one re-fetch of the host read, then a plain message |
| ADR-FE-155 | Full-size images with lazy loading and fixed dimensions; no thumbnails exist |
| ADR-FE-156 | The host owns the uploaded-file list state; the uploader is controlled |
| ADR-FE-157 | No preview is rendered for a file the browser cannot safely display inline; it becomes a download link |
| ADR-FE-158 | The viewer never renders HTML or SVG from the store, whatever the content type says |

| # | Open question | Who | Default |
|---|---|---|---|
| OQ-1 | Should the uploader show a file the scanner rejected in red, or remove it? | Owner | Remove it after the message — a rejected file was never stored, so leaving a row implies otherwise |
| OQ-2 | Retry limit on a failed upload | This slice | Manual retry only, no automatic retry |
| OQ-3 | Should owners see the EXIF warning (HO-FIL-001 OQ-4) when uploading a photo? | Owner | Nothing is shown until stripping is decided; a warning nobody can act on is noise |

## 10. Dependencies

| Needs | From |
|---|---|
| `POST /api/staff/files?purpose=`, `POST /api/customers/me/files?purpose=`, the view model and codes | HO-FIL-001 §12.4 |
| Fetch wrapper, auth headers, toast surface | HO-ACC-002 |

## 11. Specified changes — components

```
web/components/files/FileUploader.tsx        ⭐ the control
web/components/files/FileList.tsx
web/components/files/FileThumb.tsx
web/components/files/MediaViewer.tsx
web/components/files/useFileUpload.ts
web/components/files/fileRules.ts            mirrors HO-FIL-001 §8.3
web/components/files/fileErrors.ts           §8.2 mapping
```

## 12. Mocks — `web/mocks/handlers/files/*.ts`

Handlers for: success, each violation code, a slow upload (progress assertions), an aborted upload,
and an expired signed URL. `FileScanUnavailable` and the expired URL are the two that the consuming
areas' tests will lean on, so they are fixtures, not inline mocks.

## 13. Allowed scope

- Everything under `web/components/files/` and `web/mocks/handlers/files/`.
- `web/docs/decisions.md`: ADR-FE-149 … ADR-FE-158.

## 14. Forbidden scope

| Forbidden | Why |
|---|---|
| Any page, route or layout | Consuming handovers own those |
| A second HTTP client or auth path | HO-ACC-002 owns it |
| Rendering store content as HTML or inlining an SVG from the store | ADR-FE-158; it is script |
| Bypassing the signed URL (constructing a bucket URL) | HO-FIL-001 ADR-318 — the browser never talks to GCS |
| Uploading from a component without a `purpose` | The server would refuse it; failing here is clearer |

## 15. Component test cases

| # | Case | Expect |
|---|---|---|
| C1 | Keyboard only: tab to the control, `Enter`, choose a file | Uploads; no pointer needed |
| C2 | File over the cap | Refused client-side; **no request sent** |
| C3 | `.exe` renamed `.png` | Server responds `FileContentMismatch`; the §8.2 sentence is shown |
| C4 | `FileScanUnavailable` | Service-state wording; file stays queued; **Retry** offered |
| C5 | Abort mid-upload | Request cancelled; row removed; no orphan in host state |
| C6 | Completion, failure and removal | Exactly one polite announcement each; no progress announcements |
| C7 | Viewer opened from a thumb | Focus trapped; `Esc` restores focus to the thumb |
| C8 | Image whose `urlExpiresOn` has passed | One re-fetch, then the plain message; no retry loop |
| C9 | axe on the uploader (idle, uploading, error) and the viewer | No violations |
| C10 | 320 px width | No horizontal scroll; the control is still operable |

## 16. End-to-end test cases

| # | Case | Expect |
|---|---|---|
| E1 | Staff upload a product image and see it in the form | Visible before submit; survives a validation failure elsewhere in the form |
| E2 | Upload the EICAR file | Rejected message; the file is not listed |
| E3 | Upload three files | Sequential; each shows its own progress; one failure does not cancel the others |

## 17. Implementation order

1. `fileRules.ts`, `fileErrors.ts` (pure, tested first) → 2. `useFileUpload` → 3. `<FileUploader>` →
4. `<FileList>`, `<FileThumb>` → 5. `<MediaViewer>` → 6. mocks and fixtures for the consuming areas.

## 18. Acceptance criteria

| # | Criterion | Evidence |
|---|---|---|
| A1 | One uploader exists; each consuming handover's own ACCEPTANCE verifies that it adopts it (amended 2026-09-28) | §14; C1–C10; owed: HO-EMR-002 §8.2 amendment, PhotoField conversion (§1 Amended) |
| A2 | The control is fully operable by keyboard and screen reader | C1, C6, C7, C9 |
| A3 | Every server violation code produces an actionable sentence | C3, C4, §8.2 |
| A4 | A scanner outage reads as a service state, not the person's mistake | C4 |
| A5 | Expired signed URLs recover once and then say so plainly | C8 |
| A6 | **Known cost, measured not assumed**: a boarding diary of full-size images on a 3G phone profile | **Deferred by the owner 2026-09-28** to the Phase 6 performance pass, triggered by the first `<FileThumb>` list screen (§1 Amended); HO-FIL-001 OQ-5 owns the fix |

## 19. Required gates

| # | Gate | Focus |
|---|---|---|
| 1 | ARCHITECTURE | No pages, no second HTTP client, no bucket access from the browser |
| 2 | SECURITY | ADR-FE-158 (no HTML/SVG rendering), no URL construction, no token in a query string this slice builds |
| 3 | CODE_REVIEW | Abort handling and cleanup; controlled-component contract; no memory growth per upload |
| 4 | TEST | C1–C10, E1–E3 |
| 5 | ACCEPTANCE | A1–A6 |

## 20. BLOCKED conditions — stop and report

| # | Condition |
|---|---|
| **20.1** | HO-FIL-001 §12.4's view model or codes differ from §8.2 — the mapping table is the contract, not a guess |
| **20.2** | A consuming area needs an upload behaviour this slice does not have (background upload, resumable, folder upload). Amend here; do **not** build a second uploader |
| **20.3** | Progress cannot be reported in the target browsers without a new dependency |
