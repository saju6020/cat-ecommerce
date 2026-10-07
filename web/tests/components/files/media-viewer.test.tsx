import * as React from 'react';
import { describe, expect, it, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MediaViewer } from '@/components/files/MediaViewer';
import { buildStoredFileViewModel } from '@/mocks/handlers/files/fixtures';
import type { StoredFileViewModel } from '@/lib/api/files';

function Harness({
  file,
  onRefresh,
}: {
  file: StoredFileViewModel;
  onRefresh?: () => Promise<StoredFileViewModel | null | undefined>;
}) {
  const [open, setOpen] = React.useState(false);
  return (
    <div>
      <button onClick={() => setOpen(true)}>Open viewer</button>
      <MediaViewer
        open={open}
        onOpenChange={setOpen}
        file={file}
        caption="Photo of Whiskers"
        onRefresh={onRefresh}
      />
    </div>
  );
}

describe('<MediaViewer> — HO-FIL-002 §3/§8.3', () => {
  it('renders nothing until a file is actually open', () => {
    render(<MediaViewer open={false} onOpenChange={vi.fn()} file={null} caption="x" />);
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('C7 — opens as a modal dialog, Esc closes it, and focus returns to the control that opened it', async () => {
    render(<Harness file={buildStoredFileViewModel()} />);
    const opener = screen.getByRole('button', { name: 'Open viewer' });

    await userEvent.click(opener);
    const dialog = await screen.findByRole('dialog');
    expect(dialog).toHaveAccessibleName('Photo of Whiskers');

    await userEvent.keyboard('{Escape}');
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(opener).toHaveFocus();
  });

  it('shows the caption as the image alt text, per the consuming area (§8.3)', async () => {
    render(<Harness file={buildStoredFileViewModel({ contentType: 'image/png' })} />);
    await userEvent.click(screen.getByRole('button', { name: 'Open viewer' }));
    expect(await screen.findByRole('img', { name: 'Photo of Whiskers' })).toBeInTheDocument();
  });

  it("renders a PDF via an <iframe> (the browser's own viewer), never inline HTML (ADR-FE-158)", async () => {
    render(<Harness file={buildStoredFileViewModel({ contentType: 'application/pdf' })} />);
    await userEvent.click(screen.getByRole('button', { name: 'Open viewer' }));

    const frame = await screen.findByTitle('Photo of Whiskers');
    expect(frame.tagName).toBe('IFRAME');
  });

  it('ADR-FE-157 — a type the browser cannot safely display inline becomes a download link', async () => {
    const file = buildStoredFileViewModel({
      contentType: 'application/zip',
      originalFileName: 'export.zip',
    });
    render(<Harness file={file} />);
    await userEvent.click(screen.getByRole('button', { name: 'Open viewer' }));

    const link = await screen.findByRole('link', { name: /download export\.zip/i });
    expect(link).toHaveAttribute('href', file.url);
    expect(link).toHaveAttribute('download', 'export.zip');
  });

  it('C8 — an image load failure re-fetches once, then falls back to the plain message with no retry loop', async () => {
    const stale = buildStoredFileViewModel({ url: 'https://example.test/stale.png' });
    const onRefresh = vi.fn().mockResolvedValue(null);
    render(<Harness file={stale} onRefresh={onRefresh} />);
    await userEvent.click(screen.getByRole('button', { name: 'Open viewer' }));

    fireEvent.error(await screen.findByRole('img', { name: 'Photo of Whiskers' }));
    await waitFor(() =>
      expect(
        screen.getByText('This file could not be loaded — refresh the page.'),
      ).toBeInTheDocument(),
    );
    expect(onRefresh).toHaveBeenCalledTimes(1);
  });

  it('gate-fix 2026-09-28 (SECURITY MEDIUM) — a host-driven prop update to a new URL for the SAME fileId does not refresh again if it also fails', async () => {
    const original = buildStoredFileViewModel({
      fileId: 'file-1',
      url: 'https://example.test/original.png',
    });
    const onRefresh = vi.fn();

    const { rerender } = render(<Harness file={original} onRefresh={onRefresh} />);
    await userEvent.click(screen.getByRole('button', { name: 'Open viewer' }));
    fireEvent.error(await screen.findByRole('img', { name: 'Photo of Whiskers' }));
    await waitFor(() => expect(onRefresh).toHaveBeenCalledTimes(1));

    // The host independently re-fetches its own record and passes a NEW url for the same file,
    // via a prop update rather than this component's own state.
    const refetchedButStillBad = buildStoredFileViewModel({
      fileId: 'file-1',
      url: 'https://example.test/refetched-but-still-bad.png',
    });
    rerender(<Harness file={refetchedButStillBad} onRefresh={onRefresh} />);

    await waitFor(() =>
      expect(screen.getByRole('img', { name: 'Photo of Whiskers' })).toHaveAttribute(
        'src',
        refetchedButStillBad.url,
      ),
    );
    fireEvent.error(screen.getByRole('img', { name: 'Photo of Whiskers' }));

    await waitFor(() =>
      expect(
        screen.getByText('This file could not be loaded — refresh the page.'),
      ).toBeInTheDocument(),
    );
    expect(onRefresh).toHaveBeenCalledTimes(1);
  });

  it('FIL 11 — a PDF iframe load failure uses the same refresh-once guard, then the plain message', async () => {
    const pdf = buildStoredFileViewModel({
      contentType: 'application/pdf',
      url: 'https://example.test/lab.pdf',
    });
    const onRefresh = vi.fn().mockResolvedValue(pdf);
    render(<Harness file={pdf} onRefresh={onRefresh} />);
    await userEvent.click(screen.getByRole('button', { name: 'Open viewer' }));

    fireEvent.error(await screen.findByTitle('Photo of Whiskers'));
    await waitFor(() => expect(onRefresh).toHaveBeenCalledTimes(1));

    // The refreshed frame fails too — no second refresh, just the plain message.
    fireEvent.error(await screen.findByTitle('Photo of Whiskers'));
    await waitFor(() =>
      expect(
        screen.getByText('This file could not be loaded — refresh the page.'),
      ).toBeInTheDocument(),
    );
    expect(onRefresh).toHaveBeenCalledTimes(1);
  });

  it('FIL 17 — a closed viewer never calls onRefresh, even with an expired file kept set; opening it does', async () => {
    const expired = buildStoredFileViewModel({
      urlExpiresOn: new Date(Date.now() - 60_000).toISOString(),
    });
    const onRefresh = vi.fn().mockResolvedValue(null);
    const { rerender } = render(
      <MediaViewer
        open={false}
        onOpenChange={vi.fn()}
        file={expired}
        caption="x"
        onRefresh={onRefresh}
      />,
    );
    // let any (wrongly) started effect/promise settle
    await new Promise((resolve) => setTimeout(resolve, 20));
    expect(onRefresh).not.toHaveBeenCalled();

    rerender(
      <MediaViewer open onOpenChange={vi.fn()} file={expired} caption="x" onRefresh={onRefresh} />,
    );
    await waitFor(() => expect(onRefresh).toHaveBeenCalledTimes(1));
  });
});
