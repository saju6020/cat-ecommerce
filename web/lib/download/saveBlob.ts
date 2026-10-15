/**
 * `lib/download/saveBlob.ts` — HO-RPT-002 ADR-FE-132. Hands the browser a file the SERVER produced:
 * an object URL on a temporary `download` anchor, revoked immediately after the click starts the
 * download. Nothing is generated in the browser, and the blob is never stored anywhere.
 */
export function saveBlob(blob: Blob, filename: string): void {
  const url = URL.createObjectURL(blob);
  try {
    const link = document.createElement('a');
    link.href = url;
    link.download = filename;
    document.body.appendChild(link);
    link.click();
    link.remove();
  } finally {
    URL.revokeObjectURL(url);
  }
}
