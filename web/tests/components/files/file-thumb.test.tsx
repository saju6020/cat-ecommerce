import { describe, expect, it, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { FileThumb } from '@/components/files/FileThumb';
import { buildStoredFileViewModel } from '@/mocks/handlers/files/fixtures';

describe('<FileThumb> — HO-FIL-002 §3/§8.5, ADR-FE-154/155', () => {
  it('renders a real image at display size, lazy and async-decoded (no thumbnail derivative exists)', () => {
    const file = buildStoredFileViewModel({ contentType: 'image/png' });
    render(<FileThumb file={file} alt="Photo of Whiskers" width={64} height={64} />);

    const img = screen.getByRole('img', { name: 'Photo of Whiskers' });
    expect(img).toHaveAttribute('src', file.url);
    expect(img).toHaveAttribute('loading', 'lazy');
    expect(img).toHaveAttribute('decoding', 'async');
    expect(img).toHaveAttribute('width', '64');
    expect(img).toHaveAttribute('height', '64');
  });

  it('shows a type-appropriate label, never a broken-image icon, for a non-image file', () => {
    const file = buildStoredFileViewModel({
      contentType: 'application/pdf',
      originalFileName: 'lab.pdf',
    });
    render(<FileThumb file={file} alt="Lab result" />);

    expect(screen.queryByRole('img')).not.toBeInTheDocument();
    expect(screen.getByText('PDF')).toBeInTheDocument();
  });

  it('C8 — an image load failure re-fetches once via onRefresh and shows the fresh file', async () => {
    const stale = buildStoredFileViewModel({ url: 'https://example.test/stale.png' });
    const fresh = buildStoredFileViewModel({ url: 'https://example.test/fresh.png' });
    const onRefresh = vi.fn().mockResolvedValue(fresh);

    render(<FileThumb file={stale} alt="Photo" onRefresh={onRefresh} />);
    fireEvent.error(screen.getByRole('img', { name: 'Photo' }));

    await waitFor(() => expect(onRefresh).toHaveBeenCalledTimes(1));
    await waitFor(() =>
      expect(screen.getByRole('img', { name: 'Photo' })).toHaveAttribute('src', fresh.url),
    );
  });

  it('C8 — a second failure (after one refresh already happened) shows the plain message, no retry loop', async () => {
    const stale = buildStoredFileViewModel({ url: 'https://example.test/stale.png' });
    const stillBad = buildStoredFileViewModel({ url: 'https://example.test/still-bad.png' });
    const onRefresh = vi.fn().mockResolvedValue(stillBad);

    render(<FileThumb file={stale} alt="Photo" onRefresh={onRefresh} />);
    fireEvent.error(screen.getByRole('img', { name: 'Photo' }));
    await waitFor(() =>
      expect(screen.getByRole('img', { name: 'Photo' })).toHaveAttribute('src', stillBad.url),
    );

    fireEvent.error(screen.getByRole('img', { name: 'Photo' }));
    await waitFor(() =>
      expect(
        screen.getByText('This file could not be loaded — refresh the page.'),
      ).toBeInTheDocument(),
    );
    expect(onRefresh).toHaveBeenCalledTimes(1); // never called a second time
  });

  it('gate-fix 2026-09-28 (SECURITY MEDIUM) — a host-driven prop update to a new URL for the SAME fileId does not refresh again if it also fails', async () => {
    const original = buildStoredFileViewModel({
      fileId: 'file-1',
      url: 'https://example.test/original.png',
    });
    const onRefresh = vi.fn();

    const { rerender } = render(<FileThumb file={original} alt="Photo" onRefresh={onRefresh} />);
    fireEvent.error(screen.getByRole('img', { name: 'Photo' }));
    await waitFor(() => expect(onRefresh).toHaveBeenCalledTimes(1));
    // Simulate the host independently re-fetching its own record (e.g. a TanStack Query refetch
    // triggered by something other than this component's own `onRefresh` callback) and passing a
    // NEW url for the exact same file, via a prop update rather than this component's own state.
    const refetchedButStillBad = buildStoredFileViewModel({
      fileId: 'file-1',
      url: 'https://example.test/refetched-but-still-bad.png',
    });
    rerender(<FileThumb file={refetchedButStillBad} alt="Photo" onRefresh={onRefresh} />);

    await waitFor(() =>
      expect(screen.getByRole('img', { name: 'Photo' })).toHaveAttribute(
        'src',
        refetchedButStillBad.url,
      ),
    );
    fireEvent.error(screen.getByRole('img', { name: 'Photo' }));

    await waitFor(() =>
      expect(
        screen.getByText('This file could not be loaded — refresh the page.'),
      ).toBeInTheDocument(),
    );
    // The guard is keyed on fileId alone (never reset by the url-changing prop update above) — a
    // second, still-failing URL for the SAME file must not spend a second refresh attempt.
    expect(onRefresh).toHaveBeenCalledTimes(1);
  });

  it('with no onRefresh, a load failure goes straight to the plain message', async () => {
    const file = buildStoredFileViewModel();
    render(<FileThumb file={file} alt="Photo" />);
    fireEvent.error(screen.getByRole('img', { name: 'Photo' }));

    await waitFor(() =>
      expect(
        screen.getByText('This file could not be loaded — refresh the page.'),
      ).toBeInTheDocument(),
    );
  });
});
