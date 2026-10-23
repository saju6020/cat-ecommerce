GATE: SECURITY
HANDOVER: HO-FIL-001 (owner-approved follow-up ADR-370, amends ADR-318; owner approval 2026-10-18)
COMMITS: 914ab5f0 (master...feat/direct-image-urls, worktree D:/my-repo/cat_ecommerce-img)
DATE: 2026-10-18
STATUS: PASS

No BLOCKER or HIGH findings. The MEDIUM and LOW items below do not block (gate rule, owner 2026-09-27). They go to docs/backlog/FIL.md.

The review covered commit 914ab5f0. The worktree also holds uncommitted gate-fix edits (CODE_REVIEW/ARCHITECTURE). Where those edits bear on a finding, the finding says so. They were not reviewed as a commit.

FINDINGS
- [MEDIUM] src/Infrastructure/CatEcommerce.Infrastructure/Files/SkiaImageVariantGenerator.cs:30-38, 99 (as committed): decompression bomb.
  - Only JPEG decodes at a reduced scale. PNG and WebP decode at the dimensions the file declares.
  - A deflate-compressed PNG of about 1-3 MB can declare 16000x16000 or more. That makes `new SKBitmap(info)` about 1 GB, and `Upright()` then calls `source.Copy()` on the identity path, adding about 1 GB more.
  - The 5 MB FileRules cap does not bound memory, despite what the class comment says.
  - Exploit: a ShopManager or Administrator, or anyone who has taken over such an account, POSTs a few such PNGs to /api/staff/files?purpose=ProductImage. The e2-medium VM (4 GB) hits OOM, which takes down the API and possibly co-hosted Postgres.
  - If an oversized photo was stored before ADR-370 and `BackfillVariants=true`, the backfill worker (Parallelism 4) hits the same OOM at every start.
  - Privileged actor only, so MEDIUM.
  - Fix: before allocating, refuse when the decoded width*height exceeds a cap, and do not copy on the identity orientation. The uncommitted worktree edit does both: `MaxDecodedPixels = 50_000_000` on the scaled size, and `Upright` returns null for identity. Commit it with a test: a small PNG that declares about 20000x20000 must be refused without allocating.
- [LOW] src/Infrastructure/CatEcommerce.Infrastructure/Files/GcsDirectFileUrlSigner.cs:378 (committed) / FileStorageOptions.DirectServePurposes: any purpose can be switched to direct serving by configuration alone, for example `FileStorage__DirectServePurposes__0=ClinicalPhoto`.
  - Such files would then go out as bucket bearer URLs for up to two windows.
  - They would lose the app's nosniff, the `default-src 'none'; sandbox` CSP, Referrer-Policy and the per-IP FileServe rate limit.
  - ADR-370 says that adding a purpose needs its own review, but nothing in code enforces this.
  - Fix: a code-level allow-list of purposes cleared for direct serving (today only ProductImage). Fail startup on any other configured value.
- [LOW] src/Infrastructure/CatEcommerce.Infrastructure/Files/GcsDirectFileUrlSigner.cs:411 (committed): the window is clamped to 5-720 minutes. A bearer URL can therefore live up to 24 h, not the "at most 1 hour" that ADR-370 states.
  - The content is public catalogue photos only, so the impact is low.
  - The uncommitted edit clamps to 5-30 minutes, which keeps a URL to an hour or less. Commit it.
- [LOW] src/Api/CatEcommerce.Api/Background/ImageVariantBackfillWorker.cs:121-137: race between the backfill and erasure.
  - If StoredFileEraser deletes the original and its copies after the backfill has read the original but before `PutVariantAsync`, the backfill writes new copies for a file that no longer has a row.
  - Those orphan objects are invisible to retention and erasure. They hold only product-photo pixels, with no personal data.
  - Fix: after writing, check that the original still exists (`ExistsAsync(key)`) and delete the copies if it does not.
- [LOW] Direct URLs bypass the per-IP `file-serve` rate limit. A harvested URL (valid up to two windows) can be fetched in a loop against bucket egress billing. The copies are small (about 30 KB), so this is a cost risk only. Fix, if needed: put Cloud CDN in front, or set a billing alert on bucket egress.
- [LOW] .gitignore: the `gcp-key/` ignore rule exists only as an uncommitted edit in the main worktree. It is absent on this branch.
  - The diff contains no key material, and the img worktree has no gcp-key/ folder. Signing uses ADC or IAM signBlob, with no key file (correct).
  - Fix: commit the ignore rule so that every worktree and clone has it.

VERIFIED (no finding)
- Access control: the decision to direct-serve comes from the stored row's `Purpose` (DB), never from a caller. Clinical, consent, boarding, report and cat-photo reads through StoredFileSigningBatcher stay on HMAC app-served URLs. Only ShopManager/Administrator can upload ProductImage; customers cannot.
- Forced `response-content-type` and `response-content-disposition`. ProductImage allows only jpeg/png/webp, all checked by magic bytes; there is no SVG. The bytes come from storage.googleapis.com, which holds no site cookies. The missing nosniff/sandbox headers are acceptable for this purpose.
- Cache key `direct-file-url|bucket|storageKey|contentType|disposition|windowStart`: every part is server-generated or comes from a closed set, and the variant is part of the storage key. No collision is possible between original and copy, or across purposes.
- Deletion: `DeleteAsync` (GCS and Local) deletes the original and its copies. Copies are found from the key prefix, which matches StorageKeyFactory `{purpose}/...`, and no other key format exists. All deletion paths go through `IFileStore.DeleteAsync`: StoredFileEraser, the purge source, and the FilesController compensations (copy-put failure, copy-put exception, registration failure or exception), all with `CancellationToken.None`. A partial GCS delete throws, the row is kept, and a retry is idempotent.
- Upload ordering: scan, then decode, then store. An undecodable image is refused and recorded, and the temp file is deleted in `finally`.
- Secrets: no key files or credentials in the diff. `UrlSigner` comes from `GoogleCredential.GetApplicationDefault()` and is created lazily, never in Development or Testing.
- Dependencies: SkiaSharp 3.119.1 was already present (captcha). No new packages.

SCOPE CHECK
- files in allowed scope: 29/29 (FIL files plus the ADR-370-authorised Shop read helpers and tests; the gate itself wrote only this file)
- files outside allowed scope: none
