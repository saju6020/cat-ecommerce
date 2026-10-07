# File Storage — UX

The design for backlog §17 **US-XCT-07** (safe uploads), built against
[`HO-FIL-002`](../../handover/HO-FIL-002.md), the frontend handover. Fifth area, after
[Cat Profiles](../cat-profile/README.md), [Accounts & Identity](../accounts-identity/README.md),
[Administration](../administration/README.md) and [Notifications](../notifications/README.md).

## Where to review it

Review locally: http://127.0.0.1:8080/file-storage/index.html (served from docs/design)

There is no hosted canvas for this area. `index.html` lists every artboard with a one-line
description of what it shows.

## Where the source is

The `.dc.html` files in this folder are the **source of truth**. Each loads `./support.js`, the
local runtime copied byte-for-byte from `../_shared/`, which renders the artboard and swaps staff
screens' placeholder top bar for the shared sidebar and header.

## The screens

Three rows, fifteen artboards. HO-FIL-002 owns **no page or route** (§14): it builds the one
uploader, file list, thumbnail and viewer that Shop, Clinical records, Boarding and Reports import.
So these artboards are the components' states, each drawn inside an illustrative host page from a
consuming handover so they're seen at real size and in real context. The host pages are not
designs for those areas.

| Row | File | Screen | Shows |
|---|---|---|---|
| Uploader — `FileUploader`, `useFileUpload` | `UploaderIdle.dc.html` | Idle, focus & drag-over | The drop zone as a real `<button>` over a hidden file input, the purpose's type/size hint, keyboard focus and drag-over side by side (C1, ADR-FE-152) |
| | `UploaderQueue.dc.html` | ⭐ Queue & progress | Uploaded / Uploading (bytes and %) / Waiting, one file at a time; the host's disabled submit with its reason; the live-region transcript with progress deliberately not announced (E3, ADR-FE-150, 153) |
| | `UploaderRefusedClientSide.dc.html` | Refused before sending | `FileTooLarge` and `FileTypeNotAllowed` in their §8.2 wording, with a network panel showing no request was sent (C2, ADR-FE-149) |
| | `UploaderServerRefused.dc.html` | Refused by the server | `FileContentMismatch` (row kept, Remove) beside `FileRejected` in two steps: the message, then the row gone (C3, E2, OQ-1) |
| | `UploaderScanUnavailable.dc.html` | ⭐ Scanner unavailable | `FileScanUnavailable` as an amber service state: files stay queued, manual Retry, the rest of the form still works (C4, A4, ADR-FE-151) |
| | `UploaderInterrupted.dc.html` | Interrupted & cancelled | "did not finish uploading" with Retry while the next file carries on; Cancel before and after, with the row removed and one announcement (C5, OQ-2) |
| | `UploaderInForm.dc.html` | Uploads survive a failed save | E1: a product form refuses to save; the uploaded photos stay (ADR-FE-156) |
| | `Mobile360.dc.html` | Uploader at 360px | Stacked rows, full-width buttons, truncated camera file names, no sideways scroll, also checked at 320px (C10) |
| File list & thumbnails — `FileList`, `FileThumb` | `FileList.dc.html` | ⭐ File list | Open vs download-only (DICOM), a finalised visit's Remove disabled with its reason, the written-out empty state (ADR-FE-157) |
| | `FileRemoveConfirm.dc.html` | Remove confirmation | Names the file and the consequence (off the order, out of the owner's portal); the safe button is focused first |
| | `FileThumbStates.dc.html` | Thumbnail states | Loaded, loading at fixed size, video, PDF, download-only, unsafe type never rendered, could-not-load, missing caption (ADR-FE-155, 157, 158) |
| Viewer & signed URLs — `MediaViewer` | `MediaViewerImage.dc.html` | Viewer — image | Modal dialog: the caption as alt text, focus on Close, `Esc`, focus back to the opening thumb (C7) |
| | `MediaViewerPdf.dc.html` | Viewer — PDF | Paged in the browser's own PDF viewer inside the dialog, with the no-inline-PDF fallback beside it |
| | `MediaViewerVideo.dc.html` | Viewer — video (portal) | Customer-facing: native `controls`, no autoplay |
| | `ExpiredUrl.dc.html` | ⭐ Expired links (portal) | A diary left open past its 5-minute links: one silent re-fetch, then the plain message, and no retry loop (C8, A5, ADR-FE-154) |

## The design system

Built on the PurrCare design system, with **no new component**. The uploader's pieces are
put together from what the system already has: `Field` for labels and hints, `Button` in its
small and danger forms, `Banner`'s shape for the scanner-outage line, `Dialog` for the viewer and
the remove confirmation, and `Badge`'s icon-plus-word rule for every file status. One pattern is
new here and could be promoted into the system if a second area needs it: the **file row** (type
tile, name, size, status line, optional `<progress>`, actions).

As with the earlier areas, token values are copied by hand into each artboard rather than
loaded from one shared stylesheet.

## Rules the design encodes

1. **Status is never colour alone.** Every file state is a word and an icon: Uploaded, Uploading,
   Waiting, Queued, Not uploaded, Stopped. Refused rows also get a thick left edge.
2. **The server's message is never shown raw.** Every violation code maps to a §8.2 sentence
   that names the file and says what the person can do (ADR-FE-151).
3. **A scanner outage is a service state, not the person's mistake.** It is amber, it says
   "paused", and the files wait in the queue (A4).
4. **Nothing is announced per progress tick.** Each board with a live region shows exactly one
   polite announcement per finished, failed or removed file (ADR-FE-153).
5. **Consequences are named before they happen.** Removing an attached file says what stops
   happening. Cancelling an unfinished upload doesn't ask for confirmation, because nothing has
   been saved yet, and the board says why.
6. **Disabled controls explain themselves.** "Attach results" waits for uploads and says so. A
   finalised visit's Remove says why it's locked and what to do instead.
7. **Missing data is written out.** "No files attached to this order.", "No caption recorded",
   "No preview frame". A tile is never blank and never shows a broken image.
8. **Units sit next to numbers, and times name their zone.** "4.0 MB of 6.4 MB (62%)" and
   "23 Sep 2026, 10:48 (Asia/Dhaka)".
9. **The browser never renders HTML or SVG from the store, and never builds a bucket URL**
   (ADR-FE-158, HO-FIL-001 ADR-318).

## Known gaps, deliberately visible

- **No live interaction.** As in the other areas, every state is its own artboard.
- **Host pages are placeholders.** The product form, diagnostic order, photos panel and boarding
  diary come from HO-SHP-002, HO-EMR-002 and HO-BRD-002, which will design them properly. Only the
  shared components here are specified.
- **Remove confirmation wording is this design's suggestion.** HO-FIL-002 specifies Remove on
  `FileList` but not its confirmation. The consuming area owns what removal means (detach vs.
  delete) and should confirm the sentence.
- **No EXIF warning is shown on photo uploads.** This follows OQ-3's default, which waits until
  stripping is decided.
- **Thumbnails are the full-size originals** (ADR-FE-155). `FileThumbStates.dc.html` records this
  as the known cost from A6. It is not solved here.
- **Illustrations stand in for photos.** File names, sizes and times are illustrative. No backend
  exists yet.
