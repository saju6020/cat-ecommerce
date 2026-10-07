GATE: SECURITY
HANDOVER: HO-FIL-001
COMMITS: 1853cb1 d7c9bbe 6e0af33 ea4f818 1b66708 7fa3bbe 421f9c9 dbcd824 (gate-fix round), reviewed as `git diff master...ho/FIL-001` at head dbcd824
DATE: 2026-09-27
STATUS: PASS

Pass: SECOND SECURITY pass (the first pass, FAIL, is kept verbatim below). All three first-pass MEDIUMs
are closed and verified by probe. Four LOWs remain; none blocks.

How it was checked:
- Fresh `git archive dbcd824` with no git-ignored appsettings. Build: 0 errors.
- Fresh DB `catecommerce_fil2sec` via `dotnet-ef database update`. CATECOMMERCE_TEST_DB was set to it, and
  TEMP/TMP were redirected into the scratch folder, so file stores were isolated.
- Suites:
  - Application: 80/80.
  - Domain: 284/284.
  - Infrastructure: 54/54.
  - Integration: 332/332 on the first full run. The first pass's I4 isolation flake did not recur.
- Probes, kept outside the repo in scratchpad\fil1-sec2-probes: about 250 requests through TestServer.
  - The purpose/authz matrix, the signature cases and the cat-photo probes ran against a factory that
    uses the REAL DI wiring, without the test decorator.
  - A second factory copies the test-only decorator. It was used only for content and file-name checks
    on purposes that have no production mapping yet.
  - A Production-environment host start with a fake `StorageClient`.
  - `GcsFileStore` driven directly by a fake `StorageClient`.
  - A real Kestrel run (Testing environment) for the 60 MB cap, malformed multipart, client abort and
    the X-Forwarded-For checks.

First-pass items, re-verified:
- [MEDIUM, CLOSED] Purpose gate now fails closed in the real host.
  - The real host resolves `DefaultFilePurposeAccessPolicy`.
    - `UnownedPurposeAllowingPolicy` exists only in the IntegrationTests assembly (FilesApiFactory.cs:64).
      It is registered only through that factory's `ConfigureTestServices`, so no production composition
      root can reach it.
  - Staff route, all 8 roles x all 8 purposes:
    - Every purpose except `CatProfilePhoto` -> 403 `FilePurposeNotAllowedForCaller`.
    - `CatProfilePhoto` -> 201 for Reception, Nurse, Vet and Administrator (`Cats.EditProfile`); 403 for
      Caretaker, Inventory, ShopManager and Accountant.
  - Customer route: `CatProfilePhoto` -> 201; the other 7 purposes -> 403.
  - Unchanged:
    - Anonymous: 401.
    - Customer on the staff route: 403.
    - Staff on the customer route: 401.
    - No CSRF token: 403.
- [MEDIUM, CLOSED] Download file names.
  - The extension is always the canonical one for the stored, verified ContentType:
    - `cat.hta` (PNG) -> `cat.png`
    - `Quarterly-report.hta` -> `report.csv`
    - `a.exe` (zip) -> `a.zip`
    - `scan.js` (DICOM) -> `scan.dcm`
    - `v.scr` (mp4) -> `v.mp4`
    - `a.html` (PDF) -> `a.pdf`
    - `photo.png.exe` -> `photo.png.png`
    - `evil.hta.` -> `evil.hta.png`
    - `.hta` -> `file.png`
  - U+202E, U+202A and U+200B are stripped when the name is stored. `invoice<U+202E>gnp.hta` is served as
    `invoicegnp.png`. A name made only of format characters becomes `upload.png`.
  - text/csv checks: an HTA, HTML or shebang body at the start of the file -> 400 `FileContentMismatch`.
  - Traversal is still reduced to `evil.png`.
- [MEDIUM, CLOSED] `GcsFileStore.OpenReadAsync` streams through a `Pipe` with default backpressure; there is no
  MemoryStream.
  - A 60 MB fake object streamed fully (62,914,560 bytes).
  - Memory stays bounded by the pipe thresholds (code review). The measured managed delta of about
    21 MB is uncollected read-buffer garbage, not the object.
  - A missing object -> `FileObjectMissing`.
  - See LOW-2 for the error path.
- [LOW, CLOSED] Rate limits trigger, and their keys cannot be spoofed.
  - Uploads:
    - The same Reception account got 20x201 and then 429, 429.
    - A second account immediately after got 201: the partition is per caller, because
      `SessionAuthenticationMiddleware` runs before `UseRateLimiter`.
    - A customer: 20x201, then 429.
    - The 429 body is the standard `TooManyRequests` ValidationResponse, with `Retry-After: 60`.
  - Serving, 64 fetches of one signed URL with a different X-Forwarded-For on every request:
    - Untrusted peer (TestServer, fixed IP 10.9.9.9): 60x200, then 4x429.
    - Kestrel, peer 127.0.0.1: 60x200, then 4x429.
    - The XFF header is ignored because `ForwardedHeaders` is None when no trusted proxy is configured
      (HostPipelineExtensions.cs:63).
    - With the peer configured as a trusted proxy, XFF is honoured (64x200). That is the intended
      behaviour.
- [LOW, CLOSED] Compensation on throw. A register handler that throws gives 500, the object count stays
  52 -> 52 (the object is deleted), and no temp file is left.
- [LOW, CLOSED] EICAR is found anywhere in the stream; every case below -> 400 FileRejected:
  - at offset 0;
  - after 5,000 bytes;
  - straddling the 81,920-byte chunk boundary;
  - straddling the 163,840-byte chunk boundary;
  - at the end of a 3 MB file;
  - 200 KB into a PNG.
  No temp files are left.
- [LOW, CLOSED] `Cache-Control` on the wire is `no-store, private`, on 200, 403 and expired responses.
  Also on the serve route:
  - `nosniff`;
  - CSP `default-src 'none'; sandbox`;
  - `Referrer-Policy: no-referrer`.
- [LOW, PARTIALLY CLOSED] Malformed multipart -- see LOW-1.
  - Over 16 part headers -> 400.
  - A header over 16 KB -> 400 (TestServer and Kestrel).
  - Missing boundary parameter or boundary over 128 characters -> 400.
  - JSON body -> 400.
  - On Kestrel, a 61 MB non-file preamble -> 413 FileRejected (it was a 500).
  - On Kestrel, a 61 MB file part -> FileTooLarge at the 5 MB purpose cap, with no fail log.
- Signed URLs are unchanged and sound. All of these gave 403 `{}` with no-store:
  - tampered exp, id or disp;
  - flipped, empty or truncated sig;
  - `disp` case changed;
  - expired URL.
  A non-numeric `exp` gives the framework's 400 model-binding error.

New surface:
- `StoredFileAggregate.TryAttach` (StoredFileAggregate.cs:139). All of the following get one uniform 400
  `PhotoFileNotEligible`, so the refusal reason cannot be told apart:
  - customer B attaching customer A's file to B's own cat;
  - staff attaching a customer's file;
  - a customer attaching a staff member's file;
  - re-attaching an already-attached file.
  Customer A attaching their own file to customer B's cat -> 404 (OwnerScope, before the handler runs).
  - Race test: 12 concurrent attaches of one fresh file, split across two cats.
    - Result: 1x201, 1x409 (xmin concurrency on `files.stored_file`), 10x400.
    - Exactly one `cat_photo` row references the file.
    - The unique index on `cat_photo.FileId` is a second backstop.
- Signed photo URLs in `GET /api/cats/{id}`:
  - The owner customer gets inline URLs for their own cat's photos: TTL about 300 s (from
    `files.signedUrlMinutes`), and the URL fetches as image/png inline.
  - Customer A reading B's cat -> 404 with no URLs. Anonymous -> 401.
  - A log capture across upload, attach, profile read and fetch: 32 lines, containing 0 sig values, 0
    `sig=` strings, 0 original file names and 0 signing-key occurrences.
- `FilesPersonalDataEraser` (FilesPersonalDataEraser.cs:31). Erasing customer A:
  - redacted A's plain file (UploadedBy -> empty, name -> `[redacted]`);
  - left A's LegalHold file and A's RetainUntil+30d file intact, and returned
    `Held(2026-10-27, ...)`;
  - left customer B's file untouched.
  `IStoredFileEraser.EraseAsync` on a held file returns false; the object and the row both remain.
- `GcsBucketPolicyChecker` plus the startup guard. The real host was started in the Production
  environment with a fake StorageClient, once per bucket state:
  - secure bucket -> started (200 /health);
  - public-access prevention "inherited" -> startup aborted;
  - UBLA off -> startup aborted;
  - versioning on -> startup aborted;
  - soft-delete 7d -> startup aborted;
  - no IamConfiguration -> startup aborted.
  Each abort was an InvalidOperationException naming the control.
- Customer cat photos: YES, customers can again add photos of their own cats. The customer route
  accepts `CatProfilePhoto` (201). `POST /api/cats/{ownCat}/photos` with that fileId -> 201, and the photo
  is then served in the profile read. This closes the first pass's functional NOTE.

FINDINGS
- [LOW] src/Api/CatEcommerce.Api/Controllers/FilesController.cs:182/192 (carried, partly fixed) -- a body with no boundary line, or a truncated preamble, still returns 500.
  - `MultipartReader.ReadNextSectionAsync` throws a plain `System.IO.IOException` ("Unexpected end of Stream"). The try/catch at :195/:202 catches only `BadHttpRequestException` and `InvalidDataException`.
  - Reproduced on TestServer and on Kestrel: the body `garbage no boundary at all` -> 500, logged at `fail` with a stack trace by SafeExceptionHandlerMiddleware.
  - A client that aborts mid-upload is logged at `fail` as an unhandled OperationCanceledException.
  - A truncated file part is caught at :290 and recorded as a `FileRejected` audit row, which mislabels a broken upload as a security rejection.
  - Impact: any staff or customer session can inject error-log noise, within the 20-per-10-minutes upload limit. No data is exposed.
  - Fix:
    - Add `catch (IOException)` after the `BadHttpRequestException` clause around the section loop -> 400 FileRejected. (`BadHttpRequestException` is itself an IOException, so the order matters.)
    - Treat an OperationCanceledException when `RequestAborted` is set as a quiet return.
- [LOW] src/Infrastructure/CatEcommerce.Infrastructure/Files/GcsFileStore.cs:90-97 -- a download that fails partway is served as a successful, truncated file.
  - `await using var writerStream = pipe.Writer.AsStream()` (leaveOpen defaults to false) completes the PipeWriter normally when it is disposed. That disposal happens before the `catch` runs `pipe.Writer.CompleteAsync(ex)`, so the reader never sees the exception.
  - Probe: a fake StorageClient threw after 300 KB of a 1 MB object. The reader got a CLEAN EOF after 327,680 bytes.
  - In production, a network reset or the SDK's own download hash-validation failure (CRC32C/MD5) would therefore reach the client as HTTP 200 with a truncated body. The integrity check is silently defeated, for example for a lab-result PDF or DICOM file.
  - Fix:
    - Use `pipe.Writer.AsStream(leaveOpen: true)`, or complete the writer with the exception before the stream is disposed.
    - Add a unit test that a mid-stream throw surfaces as an exception on the reader, so the response is aborted rather than finished cleanly.
- [LOW] src/Api/CatEcommerce.Api/Modules/FileStorageModule.cs:145 -- the serving limit is keyed only on the client IP and is checked before the signature. Whether this matters depends on the deployment.
  - If the host runs behind a load balancer or proxy with no `RequestOrigin:TrustedProxies`/`TrustedNetworks`, every client shares the proxy's address as one partition. Leaving it unset is the safe default for spoofing, but it produces this effect.
  - Any anonymous caller can then send 60 requests a minute with junk signatures and push every user's file and photo serving into 429.
  - The same happens by accident to a clinic whose staff share one NAT address. A cat profile or product page with several images uses the budget quickly.
  - Fix:
    - Make trusted-proxy configuration a deployment prerequisite (ADR-342 checklist).
    - Consider partitioning by `{ip, fileId}`, or rejecting a bad signature before the limiter counts the request (for example, a separate, cheaper limit for 403s).
    - Size the limit to the real page fan-out.
- [LOW] src/Shared/CatEcommerce.Shared/Common/Constants/FileRules.cs:117 + FilesController.cs:52 -- the text/csv deny-list only sees the first 256 bytes (MagicByteSampleSize).
  - Probe: 360 bytes of benign CSV followed by `<HTA:APPLICATION/><script ...>` -> 201.
  - It is stored as `report.hta` and served as `attachment; filename=report.csv`, `text/csv`, `nosniff`, sandbox CSP. There is no execution path today, because the served name and type are forced, and no production role can upload `ReportExport`.
  - Recorded so that RPT (the future owner of ReportExport) does not treat the deny-list as a content guarantee.
  - Fix: scan the whole temp file for the deny-list, or drop the deny-list and rely on the forced extension and type (already effective).

NOTES (no finding)
- `GET /api/cats/{id}` sends no `Cache-Control`, yet its body carries 5-minute bearer URLs.
  - It has no validators (Last-Modified/ETag), so it is not heuristically cacheable. The risk is negligible.
  - A host-wide `no-store` on authenticated JSON would be consistent with §8.6.
- `FilesPersonalDataEraser` reports `Held(until = earliest RetainUntil)` even when a LegalHold file (held with no end date) is also present. The erasure worker simply retries on that date and is held again. This is acceptable.
- V3 (a real clamd) and a live GCS bucket are still unexercised: none is available here (§1 Amended (7)). The GCS paths were exercised through a fake `StorageClient` only.
- The dependency set is unchanged since the first pass. No new packages.

SCOPE CHECK
- files in allowed scope: 113/113 non-gate files in `master...dbcd824`, counting as allowed:
  - §13;
  - §1 Amended (1) and the 2026-09-27 Amended row (Cat photo `FileId` schema change, CatVaccine test bodies, CONTRACTS.md rows 6-8).
  The other 3 changed files are gate records. ARCHITECTURE owns the scope ruling.
- files outside allowed scope: none beyond those ruled on by ARCHITECTURE.

=====================================================================================================
FIRST PASS (kept for the record)
=====================================================================================================

GATE: SECURITY
HANDOVER: HO-FIL-001
COMMITS: cc88238 5590969 bc8a78d d193ffd aec5d3c 96f9d01 1c21a3d 5b3849a 4fcf31f 2a0ca74 — reviewed as `git diff master...ho/FIL-001` (head 2a0ca74; later gate-record commits 4fc20fd, 12e9f8f add no code)
DATE: 2026-09-27
STATUS: FAIL

Pass: first SECURITY pass. Read HO-FIL-001 in full (incl. §1 Amended), and HO-FIL-001-ARCHITECTURE.md
(pass 1) so as not to duplicate it.

How it was checked:
- Fresh `git archive 2a0ca74`, no git-ignored appsettings. Build: 0 errors.
- Fresh DB `catecommerce_fil1sec` via `dotnet-ef database update`.
- Suites:
  - Application: 79/79.
  - Domain: 277/277.
  - Infrastructure: 52/52.
  - Integration: 331/332 on the first full run. I4
    (`When_the_command_save_fails_the_object_is_deleted_again_no_orphan`) failed on objects left in the
    shared default `%TEMP%\catecommerce-file-store` by another run against another database. It passed
    9/9 on a rerun against a clean store. That is a test-isolation problem for the TEST gate, not a
    product defect.
- Probes: about 120 requests through TestServer, as a throw-away test class; kept outside the repo.
  - Anonymous and customer callers, all 8 staff roles, every purpose on both upload routes.
  - Signed-URL tampering: 17 variants.
  - Content, type and file-name tricks: 23 cases.
  - Size and multipart abuse; EICAR; log capture; the cat-photo attach rules.
  - A register handler that throws.
  - A real Kestrel run (`Testing` environment) for the 60 MB cap and malformed multipart.
- `dotnet list package --vulnerable --include-transitive` against nuget.org: no vulnerable packages
  (Google.Cloud.Storage.V1 5.0.0, nClam 9.0.0 and their transitive dependencies). The private feed
  could not be reached (NU1900).

What holds (verified):
- Authentication and CSRF on uploads:
  - Anonymous caller: 401 on both routes.
  - Customer on the staff route: 403.
  - Customer route: 403 for every purpose (fail closed).
  - Staff on the customer route: 401.
  - Missing X-CSRF-Token: 403.
- `CatProfilePhoto` is gated by `Cats.EditProfile`. Caretaker, Inventory, ShopManager and Accountant
  get 403.
- Signed URLs:
  - HMAC-SHA256 over `{fileId}|{exp}|{disp}`, compared with `FixedTimeEquals`, length checked first.
  - Tampered id, exp or disp; flipped, truncated, empty or garbage sig; expired URL: all 403 with body
    `{}` and `no-store`.
  - Accepted encoding variants (padded/std base64, uppercase GUID, `0`/`+` prefixed exp, duplicate
    `exp` query value) all decode to the same signed values, so they are harmless.
  - The signing key never appears in the logs.
- Checks run in the pipeline:
  - Declared type is checked against the allow-list and then against the leading bytes.
  - SVG, text/html, octet-stream, missing type, `image/png; charset=…`, `IMAGE/PNG`, PDF declared PNG,
    SVG declared PNG, and MZ declared zip are all refused.
  - Empty file refused.
  - The purpose cap is enforced while streaming: 5 MB + 1 gives 400 FileTooLarge, and the temp file is
    gone.
  - Chunked bodies without Content-Length work.
  - On Kestrel, a 61 MB body is refused by `[RequestSizeLimit]` (but as a 500; see LOW-1).
  - No temp files remained after any probe, including when host antivirus quarantined the EICAR temp
    file. That IOException path fails closed: 400 FileRejected plus an audit row.
- Storage key comes only from `StorageKeyFactory`. The traversal file name `..\..\..\windows\evil.png` is
  reduced to `evil.png`. `LocalFileStore.ResolvePath` also confines to the root.
- Response headers:
  - `nosniff`; CSP `default-src 'none'; sandbox`; `Referrer-Policy: no-referrer`.
  - Stored Content-Type is used; DICOM and zip are always `attachment`.
  - Quotes and semicolons in the file name are quoted safely in Content-Disposition (no header
    injection). CR/LF is stripped by the sanitiser.
- Logs: a capture across clean, mismatched and EICAR uploads plus a serve contained no file content, no
  original file names and no EICAR bytes. Audit AfterValue is `Purpose=…;Scanner=…` only.
- Cat-photo fix (the carried HO-CAT-002 item is closed). All of these get one uniform 400
  `PhotoFileNotEligible`:
  - another staff member's file;
  - the caller's own `ProductImage`;
  - a random id;
  - the customer owner attaching staff's file;
  - the caller's own 31-minute-old file.
  The legacy client-supplied `storageKey` body is refused.
- Scanner selection: `DevelopmentNoOpScanner` and `EicarTestScanner` throw outside their environment.
  Outside Development/Testing, an unset `ClamAvHost` gives `UnconfiguredMalwareScanner` (Unavailable).
  `ClamAvScanner` maps timeout, clamd error and connection failure to Unavailable (code review; no
  clamd was available).
- `DisableFormValueModelBinding` is applied per action only. It is a resource filter on the
  per-request factory list, used on the two upload actions and nowhere else, so it opens no multipart
  parsing elsewhere.

FINDINGS
- [MEDIUM] src/Shared/CatEcommerce.Shared/Common/Constants/FilePurposeAccess.cs:35 + src/Api/CatEcommerce.Api/Controllers/FilesController.cs:62 — the per-purpose gate fails open.
  - The rule: §9 OQ-7's recommended default is "the endpoint refuses a purpose the caller has no action for". `Actions.Files.Upload` is granted to `StaffRoles.All`.
  - What the code does: any unmapped purpose is uploadable by every staff role.
  - Probe result: Reception, Nurse, Vet, Caretaker, Inventory, ShopManager, Accountant and Administrator each got 201 for `LabResult`, `ClinicalPhoto`, `ConsentForm`, `ProductImage` and `BoardingMedia`. Every role can also upload `ReportExport` content (CSV, PDF, zip).
  - Exploit today: a Caretaker or Inventory clerk can put arbitrary allowed content under clinical or report purposes. They get back a 5-minute signed URL on the clinic's own origin, which they can forward. See the next finding for why that matters.
  - Latent risk: any consuming area (EMR, SHP, BRD, RPT) that attaches by `fileId` without repeating the Cat handler's purpose, uploader and age check inherits this.
  - The §1 Amended justification (I1 would fail) is a test-fixture problem. It is not a reason to widen production authorisation.
  - Remediation: deny by default. Refuse any purpose with no `StaffPurposeActions` entry (403 `FilePurposeNotAllowedForCaller`). Either register `ProductImage` provisionally to a named action (e.g. ShopManager/Administrator) with an Amended row, or register it only in the test factory. Each consuming handover then adds its own row.
- [MEDIUM] src/Shared/CatEcommerce.Shared/Common/Constants/FileNameSanitizer.cs:23 + src/Api/CatEcommerce.Api/Controllers/FilesController.cs:139 — the download file name is not reconciled with the verified content type, and Unicode bidi/format characters are kept. This contradicts NFR-SEC-5 ("never executable").
  - Probe results:
    - `text/csv` whose body is an HTA (`<HTA:APPLICATION/><script language=vbscript>…`) → 201, served `attachment; filename=Quarterly-report.hta`. The csv check is only "looks like text", so HTML, HTA, .bat and .ps1 all pass.
    - A PNG followed by VBScript, named `cat.hta` → served `inline; filename=cat.hta`. "Save image as" keeps `.hta`.
    - `invoice‮gnp.hta` → accepted. `filename*` keeps U+202E, which displays as `invoiceath.png`.
  - Scenario: a low-privilege staff member (see the previous finding) uploads an HTA as `ReportExport`. They send the clinic-domain link to a manager ("quarterly report"). The browser downloads `Quarterly-report.hta`; double-clicking it runs mshta. The same applies once EMR, BRD or RPT reads hand these URLs to other users.
  - Remediation, at serve time (and ideally when storing):
    - Replace the extension with the canonical one for the stored ContentType (`.png`, `.jpg`, `.webp`, `.pdf`, `.mp4`, `.dcm`, `.zip`, `.csv`), keeping only the stem.
    - Strip Unicode categories Cf/Cc (bidi overrides, zero-width characters).
    - Optionally restrict the stem to letters, digits, space, `-` and `_`.
- [MEDIUM] src/Infrastructure/CatEcommerce.Infrastructure/Files/GcsFileStore.cs:53 — `OpenReadAsync` downloads the whole object into a `MemoryStream` before serving. This was checked by code review only; no bucket was available.
  - The serving route is `[PublicEndpoint]`. A signed URL is a bearer token that can be replayed for 5 minutes, and the route has no rate limit.
  - One URL for a 50 MB `LabResult`, `BoardingMedia` or `ReportExport` object, fetched 40 times at once, pins about 2 GB of managed memory, and the process can run out of memory.
  - V4 ("never buffered in memory") is honoured on upload but not on the serving path in production. `LocalFileStore` streams, so tests cannot see this.
  - Remediation:
    - Stream the object to `Response.Body`: set the headers, then call `DownloadObjectAsync(bucket, key, Response.Body)`, or open a streaming media download.
    - Add a per-IP rate limiter on `GET /api/files/{fileId}/content`.
- [LOW] src/Api/CatEcommerce.Api/Controllers/FilesController.cs:164 — `MultipartReader.ReadNextSectionAsync` runs outside the try/catch, so these become unhandled 500s instead of 400/413, logged at `fail` with stack traces:
  - a body over the limit (on Kestrel: `BadHttpRequestException: Request body too large … 60000000 bytes`);
  - more than 16 part headers;
  - a part header over 16 KB;
  - a body with no boundary.
  Any staff member can flood the error log this way. Remediation: wrap the section loop and catch `BadHttpRequestException` (return its status, 413) and `InvalidDataException`/`IOException` (400 FileRejected).
- [LOW] src/Api/CatEcommerce.Api/Controllers/FilesController.cs:296 — the §8.2 step 7 compensation runs only when `SendAsync` returns `IsSuccess=false`.
  - If the save throws instead (DB outage or timeout), the stored object is orphaned.
  - Probe: a throwing handler gave 500, and objects went from 49 to 50 with no row.
  - An orphan is invisible to retention and erasure (§8.7). For clinical purposes that is a privacy gap. The CODE_REVIEW pass did not exercise this case.
  - Remediation: after `PutAsync` succeeds, track "registered", and delete the object in a catch or finally when registration did not complete.
- [LOW] src/Infrastructure/CatEcommerce.Infrastructure/Files/EicarTestScanner.cs:36 — scans only the first 4096 bytes. EICAR placed after 5000 bytes of text was accepted (201) and stored. It is Testing-only, but I2 ("infected content never reaches the bucket") is proven only for payloads at the start of the file. Remediation: scan the whole stream with a rolling window.
- [LOW] src/Api/CatEcommerce.Api/Controllers/FilesController.cs:47 — no rate or concurrency limit and no quota on uploads.
  - 30 concurrent Caretaker uploads all returned 201.
  - Combined with the 50 MB caps and the fail-open purpose gate, any staff account can fill the bucket and the temp disk.
  - Remediation: a per-account limiter policy in the existing `RateLimiting` section on both upload routes, plus a small concurrency limit.
- [LOW] src/Api/CatEcommerce.Api/Controllers/FilesController.cs:102 — sends `Cache-Control: no-store`; §8.6 specifies `private, no-store`. The effect is equivalent. Match the spec text.

CROSS-REFERENCES (already raised by ARCHITECTURE pass 1; not re-counted here)
- The ADR-318 bucket assertions and ADC resolution at startup are not built (ARCHITECTURE HIGH on
  FileStorageModule.cs:120). On depth, SECURITY's view:
  - Public-access prevention and uniform access are the controls behind A4 ("never publicly readable").
    They must be asserted at startup, or the deferral must be recorded explicitly, before any non-dev
    deployment.
  - The `"prod"` substring check at FileStorageModule.cs:130 is only a heuristic. A Staging deployment
    pointed at a production bucket named without "prod" passes it.
- CatPhoto stores a storage key, not a `FileId` (ARCHITECTURE HIGH). From a security view, exposing the
  raw storage key in `GET /api/cats/{id}` is harmless, because serving requires the file id plus a
  signature. The consequence is that cat photos cannot be served at all, and a purged object leaves a
  dangling key.
- `LegalHold` is not enforced inside `IStoredFileEraser` (ARCHITECTURE HIGH). Agreed.

NOTES (no finding)
- Customers hold `Cats.EditProfile` (CustomerActions), but no customer purpose exists. Customers
  therefore can no longer add cat photos. This fails closed; it is flagged for ACCEPTANCE as a
  functional regression.
- HO-XCT-001 should be aware of `StoredFilePurgeSource`: it applies one cutoff to every purpose,
  including `CatProfilePhoto` and clinical purposes.
- V3 (a real clamd) and the GCS paths were reviewed as code only. No clamd or bucket was available here,
  as §1 Amended (7) states.

SCOPE CHECK
- files in allowed scope: 81/81, counting as allowed:
  - §13 (Files layers, `files` migration, docker-compose);
  - §11.1 (`FileRules`/`FilePurpose` in Shared);
  - ADR-341 (the module);
  - the Cat files authorised by §1 Amended (1).
  ARCHITECTURE owns the scope ruling.
- files outside allowed scope: none beyond those already ruled on by ARCHITECTURE pass 1.
