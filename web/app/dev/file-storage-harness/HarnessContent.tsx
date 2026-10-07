'use client';

import * as React from 'react';
import { FileUploader } from '@/components/files/FileUploader';
import { FileList } from '@/components/files/FileList';
import { MediaViewer } from '@/components/files/MediaViewer';
import { armFileUploadGate, releaseFileUploadGate } from '@/mocks/handlers/files/gate';
import {
  buildExpiredStoredFileViewModel,
  buildStoredFileViewModel,
} from '@/mocks/handlers/files/fixtures';
import type { StoredFileViewModel } from '@/lib/api/files';

/**
 * The harness's actual content — split out from `page.tsx` (gate-fix 2026-09-28, ARCHITECTURE/
 * SECURITY BLOCKER/MEDIUM) so it can be `React.lazy`-loaded. `page.tsx` calls `notFound()` before
 * ever rendering this component when `NODE_ENV === 'production'`, so a production build never
 * issues the dynamic `import()` that would fetch this module (and, with it, a live
 * `<FileUploader>` wired to the real `POST /api/staff/files`, plus the mock gate/fixtures) at all.
 *
 * See `page.tsx`'s own doc comment and HO-FIL-002 §1 Amended (2026-09-28, owner decision) for the
 * full history.
 */
export default function HarnessContent() {
  const [files, setFiles] = React.useState<StoredFileViewModel[]>([]);
  const [viewerFile, setViewerFile] = React.useState<StoredFileViewModel | null>(null);
  const [viewerOpen, setViewerOpen] = React.useState(false);
  // E1 (§16) — "the uploaded file survives a validation failure elsewhere in the form": a
  // minimal stand-in for a real consuming form's own unrelated required field, so an e2e test can
  // prove `files` (host state, ADR-FE-156) is untouched by a submit that fails for a DIFFERENT
  // reason. No real submission happens here — this harness has nowhere to submit to.
  const [productName, setProductName] = React.useState('');
  const [productNameError, setProductNameError] = React.useState<string | null>(null);

  // Test-only hooks for the Playwright layer (mirrors app/providers.tsx's own `__mswReady`/
  // `__apiFetch` pattern) — MSW's browser-mode handlers run in this same page realm, so these
  // functions are the SAME module instance `mocks/handlers/files/handlers.ts` awaits on. This
  // component itself is only ever reached in non-production builds (see `page.tsx`), so the
  // `NODE_ENV` check this effect used to need is gone — the whole module is unreachable instead.
  React.useEffect(() => {
    const w = window as unknown as {
      __fileUploadGates?: { arm: typeof armFileUploadGate; release: typeof releaseFileUploadGate };
      __fileStorageHarness?: {
        addExpiredFile: () => void;
        addFreshFile: () => void;
      };
    };
    w.__fileUploadGates = { arm: armFileUploadGate, release: releaseFileUploadGate };
    w.__fileStorageHarness = {
      addExpiredFile: () =>
        setFiles((current) => [
          ...current,
          buildExpiredStoredFileViewModel({ fileId: `expired-${current.length}` }),
        ]),
      addFreshFile: () =>
        setFiles((current) => [
          ...current,
          buildStoredFileViewModel({ fileId: `fresh-${current.length}` }),
        ]),
    };
  }, []);

  function openViewer(file: StoredFileViewModel) {
    setViewerFile(file);
    setViewerOpen(true);
  }

  return (
    <main className="mx-auto flex max-w-2xl flex-col gap-6 p-6">
      <div>
        <h1 className="font-display text-xl font-semibold text-ink">File Storage — test harness</h1>
        <p className="text-sm text-ink-muted">
          Not a product page — see HO-FIL-002 §1 Amended. Mounts the shared uploader, list and
          viewer for this slice&rsquo;s own accessibility/overflow/e2e coverage.
        </p>
      </div>

      <section
        aria-labelledby="harness-uploader-heading"
        data-testid="uploader-section"
        className="flex flex-col gap-3"
      >
        <h2 id="harness-uploader-heading" className="text-sm font-semibold text-ink">
          Upload
        </h2>
        <FileUploader
          id="harness-uploader"
          label="Add product photos"
          purpose="ProductImage"
          audience="staff"
          onUploaded={(result) => setFiles((current) => [...current, result])}
        />
      </section>

      <section
        aria-labelledby="harness-files-heading"
        data-testid="file-list-section"
        className="flex flex-col gap-3"
      >
        <h2 id="harness-files-heading" className="text-sm font-semibold text-ink">
          Uploaded files
        </h2>
        <FileList
          files={files}
          emptyMessage="No files uploaded yet."
          getCaption={(file) => `Photo: ${file.originalFileName}`}
          onView={openViewer}
          onRemove={(file) =>
            setFiles((current) => current.filter((f) => f.fileId !== file.fileId))
          }
          onRefresh={async (file) =>
            buildStoredFileViewModel({
              fileId: file.fileId,
              originalFileName: file.originalFileName,
            })
          }
        />
      </section>

      <MediaViewer
        open={viewerOpen}
        onOpenChange={setViewerOpen}
        file={viewerFile}
        caption={viewerFile ? `Photo: ${viewerFile.originalFileName}` : ''}
        onRefresh={
          viewerFile
            ? async () =>
                buildStoredFileViewModel({
                  fileId: viewerFile.fileId,
                  originalFileName: viewerFile.originalFileName,
                })
            : undefined
        }
      />

      <section aria-labelledby="harness-form-heading" className="flex flex-col gap-3">
        <h2 id="harness-form-heading" className="text-sm font-semibold text-ink">
          Unrelated form field (E1)
        </h2>
        <label htmlFor="harness-product-name" className="text-sm text-ink">
          Product name
        </label>
        <input
          id="harness-product-name"
          type="text"
          value={productName}
          onChange={(event) => setProductName(event.target.value)}
          aria-describedby={productNameError ? 'harness-product-name-error' : undefined}
          aria-invalid={!!productNameError}
          className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-1.5 text-sm text-ink"
        />
        {productNameError && (
          <p
            id="harness-product-name-error"
            role="alert"
            className="text-xs font-semibold text-danger-ink"
          >
            {productNameError}
          </p>
        )}
        <button
          type="button"
          onClick={() =>
            setProductNameError(productName.trim() ? null : 'Product name is required.')
          }
          className="w-fit rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-1.5 text-sm font-semibold text-ink hover:bg-surface-sunken"
        >
          Save
        </button>
      </section>
    </main>
  );
}
