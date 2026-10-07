import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';
import { useFileUpload } from '@/lib/hooks/useFileUpload';
import { armFileUploadGate, releaseFileUploadGate } from '@/mocks/handlers/files/gate';
import { fakeXhrRequests, installFakeXhr, restoreRealXhr } from '../../utils/fakeXhr';
import type { StoredFileViewModel } from '@/lib/api/files';

function makeFile(name: string, type: string, sizeInBytes = 1024): File {
  const file = new File([new Uint8Array(Math.min(sizeInBytes, 4096))], name, { type });
  Object.defineProperty(file, 'size', { value: sizeInBytes });
  return file;
}

beforeEach(() => installFakeXhr());
afterEach(() => restoreRealXhr());

describe('useFileUpload — HO-FIL-002 §3/§8, ADR-FE-149/150/151', () => {
  it('C2 — a file over the cap is refused client-side; no request is sent', async () => {
    const onUploaded = vi.fn();
    const { result } = renderHook(() =>
      useFileUpload({ purpose: 'ProductImage', audience: 'staff', onUploaded }),
    );

    act(() => {
      result.current.addFiles([makeFile('huge.png', 'image/png', 6 * 1024 * 1024)]);
    });

    expect(result.current.queue).toHaveLength(1);
    expect(result.current.queue[0]?.status).toBe('error');
    expect(result.current.queue[0]?.error?.code).toBe('FileTooLarge');
    expect(result.current.queue[0]?.error?.retryable).toBe(false);
    expect(result.current.announcement.text).toMatch(/largest allowed here is 5 MB/);
    expect(fakeXhrRequests()).toHaveLength(0);
  });

  it('uploads a valid file, reports progress, and removes it from the queue on success (ADR-FE-156)', async () => {
    const onUploaded = vi.fn();
    const { result } = renderHook(() =>
      useFileUpload({ purpose: 'ProductImage', audience: 'staff', onUploaded }),
    );

    act(() => {
      result.current.addFiles([makeFile('gate-p1-photo.png', 'image/png', 2048)]);
    });

    await waitFor(() => expect(result.current.queue[0]?.status).toBe('uploading'));
    expect(fakeXhrRequests()).toHaveLength(1);

    act(() => releaseFileUploadGate('p1'));

    await waitFor(() => expect(onUploaded).toHaveBeenCalledTimes(1));
    const [resultFile, sourceFile] = onUploaded.mock.calls[0] as [StoredFileViewModel, File];
    expect(resultFile.originalFileName).toBe('gate-p1-photo.png');
    expect(sourceFile.name).toBe('gate-p1-photo.png');
    expect(result.current.queue).toHaveLength(0);
    expect(result.current.announcement.text).toBe('gate-p1-photo.png finished uploading.');
  });

  it('C4 — FileScanUnavailable reads as a service state and keeps the file queued with Retry, which then succeeds', async () => {
    const onUploaded = vi.fn();
    const { result } = renderHook(() =>
      useFileUpload({ purpose: 'ProductImage', audience: 'staff', onUploaded }),
    );

    act(() => {
      result.current.addFiles([makeFile('flaky-scan-once-vaccine-card.png', 'image/png', 2048)]);
    });

    await waitFor(() => expect(result.current.queue[0]?.status).toBe('error'));
    expect(result.current.queue[0]?.error?.code).toBe('FileScanUnavailable');
    expect(result.current.queue[0]?.error?.retryable).toBe(true);
    expect(result.current.announcement.text).toMatch(/virus scanner is unavailable/);

    act(() => result.current.retry(result.current.queue[0]!.id));

    await waitFor(() => expect(onUploaded).toHaveBeenCalledTimes(1));
    expect(result.current.queue).toHaveLength(0);
  });

  it('OQ-1 — a scanner-rejected file is removed after its message, not left as a lingering row', async () => {
    const onUploaded = vi.fn();
    const { result } = renderHook(() =>
      useFileUpload({ purpose: 'ProductImage', audience: 'staff', onUploaded }),
    );

    act(() => {
      result.current.addFiles([makeFile('reject-eicar.png', 'image/png', 2048)]);
    });

    await waitFor(() =>
      expect(result.current.announcement.text).toMatch(/rejected by the virus scanner/),
    );
    expect(result.current.queue).toHaveLength(0);
    expect(onUploaded).not.toHaveBeenCalled();
  });

  it('C5 — cancelling mid-upload removes the row, with no orphan and no error state', async () => {
    const onUploaded = vi.fn();
    const { result } = renderHook(() =>
      useFileUpload({ purpose: 'ProductImage', audience: 'staff', onUploaded }),
    );

    armFileUploadGate('p2');
    act(() => {
      result.current.addFiles([makeFile('gate-p2-photo.png', 'image/png', 2048)]);
    });
    await waitFor(() => expect(result.current.queue[0]?.status).toBe('uploading'));

    act(() => result.current.remove(result.current.queue[0]!.id));

    expect(result.current.queue).toHaveLength(0);
    expect(result.current.announcement.text).toBe('gate-p2-photo.png removed.');
    // Never resolved — releasing after removal must not resurrect it or call onUploaded.
    releaseFileUploadGate('p2');
    await new Promise((resolve) => setTimeout(resolve, 0));
    expect(onUploaded).not.toHaveBeenCalled();
    expect(result.current.queue).toHaveLength(0);
  });

  it('E3/ADR-FE-150 — three files upload sequentially; one failing does not cancel the others', async () => {
    const onUploaded = vi.fn();
    const { result } = renderHook(() =>
      useFileUpload({ purpose: 'ProductImage', audience: 'staff', onUploaded }),
    );

    armFileUploadGate('a');
    armFileUploadGate('b');
    act(() => {
      result.current.addFiles([
        makeFile('gate-a-one.png', 'image/png', 100),
        makeFile('reject-two.png', 'image/png', 100),
        makeFile('gate-b-three.png', 'image/png', 100),
      ]);
    });

    // Only the first is in flight — sequential, not parallel.
    await waitFor(() =>
      expect(result.current.queue.find((i) => i.file.name === 'gate-a-one.png')?.status).toBe(
        'uploading',
      ),
    );
    expect(fakeXhrRequests()).toHaveLength(1);
    expect(result.current.queue.find((i) => i.file.name === 'gate-b-three.png')?.status).toBe(
      'queued',
    );

    act(() => releaseFileUploadGate('a'));
    await waitFor(() => expect(onUploaded).toHaveBeenCalledTimes(1));

    // The rejected file is handled (and removed) without blocking the third.
    await waitFor(() =>
      expect(result.current.queue.find((i) => i.file.name === 'gate-b-three.png')?.status).toBe(
        'uploading',
      ),
    );
    expect(result.current.queue.find((i) => i.file.name === 'reject-two.png')).toBeUndefined();

    act(() => releaseFileUploadGate('b'));
    await waitFor(() => expect(onUploaded).toHaveBeenCalledTimes(2));
    expect(result.current.queue).toHaveLength(0);
  });
});
