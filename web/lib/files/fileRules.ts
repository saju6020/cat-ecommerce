import type { FilePurpose } from '@/lib/api/files';

/**
 * `FileRules` (HO-FIL-002 §8.1/§8.3, ADR-FE-149) — mirrors `FileRules.cs`
 * (`src/Shared/CatEcommerce.Shared/Common/Constants/FileRules.cs`) byte-for-byte: same purposes,
 * same allow-lists, same caps. **The server is still the authority** — this table exists only so
 * a person is refused instantly, before a single byte leaves the machine, rather than after a
 * slow upload the server was always going to reject anyway (ADR-FE-149). A mismatch between this
 * file and the backend's is a defect in this file, not a rule to relax.
 *
 * **Location, gate-fix 2026-09-28 (ARCHITECTURE MEDIUM).** HO-FIL-002 §11 names this file
 * `components/files/fileRules.ts`. It lives in `lib/files/` instead: `lib/hooks/useFileUpload.ts`
 * is its main caller, and a `lib/` module importing from `components/` is the one `lib` →
 * `components` dependency this app would otherwise have (`web/README.md`'s layering is one
 * direction only). `components/files/*` still imports this module — that direction is fine.
 * Recorded in HO-FIL-002 §1 Amended.
 */
export interface FilePurposeRule {
  purpose: FilePurpose;
  /** Declared MIME types this purpose accepts. The server also checks the file's actual leading
   *  bytes agree (`FileContentMismatch`) — this table cannot and does not replicate that check;
   *  it only mirrors the declared-type allow-list and the size cap (ADR-FE-149's "courtesy,
   *  never trust"). */
  allowedContentTypes: readonly string[];
  maxSizeInBytes: number;
}

const ONE_MB = 1024 * 1024;

export const FILE_RULES: Readonly<Record<FilePurpose, FilePurposeRule>> = {
  LabResult: {
    purpose: 'LabResult',
    allowedContentTypes: ['application/pdf', 'image/jpeg', 'image/png', 'application/dicom'],
    maxSizeInBytes: 50 * ONE_MB,
  },
  ClinicalPhoto: {
    purpose: 'ClinicalPhoto',
    allowedContentTypes: ['image/jpeg', 'image/png', 'image/webp'],
    maxSizeInBytes: 15 * ONE_MB,
  },
  ConsentForm: {
    purpose: 'ConsentForm',
    allowedContentTypes: ['application/pdf', 'image/jpeg', 'image/png'],
    maxSizeInBytes: 15 * ONE_MB,
  },
  ProductImage: {
    purpose: 'ProductImage',
    allowedContentTypes: ['image/jpeg', 'image/png', 'image/webp'],
    maxSizeInBytes: 5 * ONE_MB,
  },
  BoardingMedia: {
    purpose: 'BoardingMedia',
    allowedContentTypes: ['image/jpeg', 'image/png', 'image/webp', 'video/mp4'],
    maxSizeInBytes: 50 * ONE_MB,
  },
  BoardingBelongings: {
    purpose: 'BoardingBelongings',
    allowedContentTypes: ['image/jpeg', 'image/png', 'image/webp'],
    maxSizeInBytes: 15 * ONE_MB,
  },
  ReportExport: {
    purpose: 'ReportExport',
    allowedContentTypes: ['application/pdf', 'text/csv', 'application/zip'],
    maxSizeInBytes: 50 * ONE_MB,
  },
  // Integrator-authorised addition on HO-FIL-001 (§1 Amended, 2026-09-26) — cap mirrors the
  // pre-existing CatPhotoRules the Cat Profiles aggregate already enforced (5 MB). Listed here
  // for completeness/accuracy; no page in this repo consumes `purpose="CatProfilePhoto"` through
  // this slice yet — see HO-FIL-002's own §1 Amended row on the cat-photo page.
  CatProfilePhoto: {
    purpose: 'CatProfilePhoto',
    allowedContentTypes: ['image/jpeg', 'image/png', 'image/webp'],
    maxSizeInBytes: 5 * ONE_MB,
  },
};

export function findFileRule(purpose: FilePurpose): FilePurposeRule {
  return FILE_RULES[purpose];
}

/**
 * ADR-FE-158, gate-fix 2026-09-28 (ARCHITECTURE MEDIUM) — `<FileThumb>`/`<MediaViewer>` must never
 * render `image/svg+xml` as an `<img>`: SVG is script, not an image this app trusts inline
 * (HO-FIL-001 §8.3's own "no SVG, it is script"). No purpose's `FileRules` row allows it today, so
 * this exclusion is currently unreachable in practice — but the ADR's own title ("whatever the
 * content type says") promises the viewer enforces it itself, not only the upload allow-list, so
 * both components call this instead of a bare `startsWith('image/')` check.
 */
export function isRenderableImageContentType(contentType: string): boolean {
  return contentType.startsWith('image/') && contentType !== 'image/svg+xml';
}

const CONTENT_TYPE_LABELS: Record<string, string> = {
  'application/pdf': 'PDF',
  'image/jpeg': 'JPEG',
  'image/png': 'PNG',
  'image/webp': 'WebP',
  'application/dicom': 'DICOM',
  'video/mp4': 'MP4 video',
  'text/csv': 'CSV',
  'application/zip': 'ZIP',
};

/** A human label for a MIME type, for a sentence a person can act on (§8.2) — never the raw MIME
 *  string. Falls back to the MIME string itself for a type this table doesn't know, which can
 *  only happen for a type no purpose allows anyway. */
export function humanizeContentType(contentType: string): string {
  return CONTENT_TYPE_LABELS[contentType] ?? contentType;
}

/** Joins allowed-type labels the way §8.2's sentence reads them: "JPEG, PNG or WebP". */
export function describeAllowedTypes(purpose: FilePurpose): string {
  const labels = findFileRule(purpose).allowedContentTypes.map(humanizeContentType);
  if (labels.length === 0) return '';
  if (labels.length === 1) return labels[0]!;
  return `${labels.slice(0, -1).join(', ')} or ${labels[labels.length - 1]}`;
}

/** `"50 MB"`, `"823 KB"`, `"1.4 MB"` — one decimal place above 1 MB, whole numbers below it.
 *  Good enough for the §8.2 sentences; this is presentation, not a spec'd wire format. */
export function formatFileSize(bytes: number): string {
  if (bytes >= ONE_MB) {
    const mb = bytes / ONE_MB;
    return `${Number.isInteger(mb) ? mb : mb.toFixed(1)} MB`;
  }
  if (bytes >= 1024) {
    return `${Math.round(bytes / 1024)} KB`;
  }
  return `${bytes} B`;
}

export interface ClientValidationFailure {
  code: 'FileTooLarge' | 'FileTypeNotAllowed';
  fileName: string;
}

/**
 * ADR-FE-149: refused instantly, before a request is ever sent (C2). Returns `null` when the
 * file passes both checks — it may still fail server-side (`FileContentMismatch`, the scan), which
 * this function cannot and does not attempt to predict.
 */
export function validateFileClientSide(
  file: File,
  purpose: FilePurpose,
): ClientValidationFailure | null {
  const rule = findFileRule(purpose);

  if (!rule.allowedContentTypes.includes(file.type)) {
    return { code: 'FileTypeNotAllowed', fileName: file.name };
  }
  if (file.size > rule.maxSizeInBytes) {
    return { code: 'FileTooLarge', fileName: file.name };
  }
  return null;
}
