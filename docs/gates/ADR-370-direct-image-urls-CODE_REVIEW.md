GATE: CODE_REVIEW
HANDOVER: ADR-370-direct-image-urls
COMMITS: 914ab5f0
DATE: 2026-10-18
STATUS: FAIL

FINDINGS
- [HIGH] src/Infrastructure/CatEcommerce.Infrastructure/Configuration/FileStorageOptions.cs (DirectServePurposes default ["ProductImage"], BackfillVariants default false) + StoredFileSigningBatcher.cs (variant key chosen with no existence check) — on first deploy every pre-ADR-370 product photo gets a signed URL to {key}.w480.webp / .w1200.webp that does not exist until someone sets FileStorage:BackfillVariants=true and the worker finishes: all catalogue/product images 404. Also permanent for any old photo the backfill cannot decode. Fix: default direct serving off until backfill is confirmed complete (or make the batcher fall back to the app-served URL when the copy is absent / backfill not finished), and document the order in the ADR runbook.
- [MEDIUM] StoredFileSigningBatcher.cs Task.WhenAll — unbounded parallel IAM signBlob calls on a cold window (one per photo on the page, every 30 min, plus cache stampede on concurrent requests); one failed signature throws and 500s the whole listing, where the HMAC signer was local and could not fail. Fix: bound concurrency (SemaphoreSlim/Parallel.ForEachAsync), catch per file and fall back to the app-served URL (or url null).
- [MEDIUM] SkiaImageVariantGenerator.cs Generate — decodes at full size for non-JPEG (PNG/WebP/GIF): codec.Info is never checked, so a small-bytes, huge-dimension PNG allocates width*height*4 (decompression bomb) in the upload request and in the backfill. Fix: reject (return null) when Width*Height exceeds a cap (e.g. 50 MP) before allocating.
- [MEDIUM] ImageVariantBackfillWorker.cs ExecuteAsync — the initial DB query is outside any try/catch; an exception (DB down, schema not migrated) escapes ExecuteAsync and the default BackgroundServiceExceptionBehavior stops the host. Fix: wrap the key query, log and return.
- [LOW] SkiaImageVariantGenerator.cs Resize — Mitchell cubic without mipmaps from a decode of >=2400 px down to 480 px (5x+) aliases. Fix: SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear), or step down in halves.
- [LOW] SkiaImageVariantGenerator.cs GetPixels — IncompleteInput is accepted, so a truncated upload is stored with a partly grey copy; consider rejecting it at upload (keep tolerant in the backfill).
- [LOW] SkiaImageVariantGenerator.cs — identity orientation does source.Copy() and fit==1 does upright.Copy() again (two needless full copies); SKImage.FromBitmap can use the bitmap directly. Upright: `turned` leaks if DrawBitmap throws.
- [LOW] GcsDirectFileUrlSigner.cs — X-Goog-Date comes from the system clock while windows come from TimeProvider (documented); the cache key includes contentType/disposition (fine). No stampede guard (see MEDIUM above).

VERIFIED CORRECT
- All seven EXIF matrices (TopRight, BottomRight, BottomLeft, LeftTop, RightTop, RightBottom, LeftBottom), checked against SKMatrix(sx,kx,tx,ky,sy,ty) ordering and the swapped output size.
- Disposal order of SKData/SKCodec/bitmaps/SKImage/SKData encode; window arithmetic (expires = windowStart + 2 windows, cache ends at window end, clamp 5-720 min stays < 7 days).
- Task.WhenAll lambdas touch no DbContext (rows are materialised first); backfill creates a scope per item; compensating deletes use CancellationToken.None, consistent with existing registration compensation; delete is idempotent and removes copies.

SCOPE CHECK
- files in allowed scope: 29/29
- files outside allowed scope: none
