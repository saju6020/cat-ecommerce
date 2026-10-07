import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { FileUploader } from '@/components/files/FileUploader';
import { armFileUploadGate, releaseFileUploadGate } from '@/mocks/handlers/files/gate';
import { installFakeXhr, restoreRealXhr } from '../../utils/fakeXhr';

function makeFile(name: string, type: string, sizeInBytes = 1024): File {
  const file = new File([new Uint8Array(Math.min(sizeInBytes, 4096))], name, { type });
  Object.defineProperty(file, 'size', { value: sizeInBytes });
  return file;
}

function getHiddenInput(container: HTMLElement): HTMLInputElement {
  const input = container.querySelector('input[type="file"]');
  if (!input) throw new Error('FileUploader did not render its hidden <input type="file">.');
  return input as HTMLInputElement;
}

/** The one visible queue row `<li>` — scopes a query away from the aria-live region, which
 *  deliberately mirrors the same text (C6). */
function getRow(): HTMLElement {
  return screen.getByRole('listitem');
}

beforeEach(() => installFakeXhr());
afterEach(() => restoreRealXhr());

describe('<FileUploader> — HO-FIL-002 component tests (§15)', () => {
  it("C1 — the drop-zone button is the only tab stop; activating it opens the hidden input's picker", async () => {
    const { container } = render(
      <FileUploader
        id="photos"
        label="Add photos"
        purpose="ProductImage"
        audience="staff"
        onUploaded={vi.fn()}
      />,
    );

    const button = screen.getByRole('button', { name: /add photos/i });
    const input = getHiddenInput(container);
    expect(input).toHaveAttribute('tabindex', '-1');
    expect(input).toHaveAttribute('aria-hidden', 'true');

    const clickSpy = vi.spyOn(input, 'click');
    await userEvent.tab();
    expect(button).toHaveFocus();
    await userEvent.keyboard('{Enter}');
    expect(clickSpy).toHaveBeenCalledTimes(1);
  });

  it('C2 — a file over the cap is refused client-side; shown inline, no Retry offered', async () => {
    const { container } = render(
      <FileUploader
        id="photos"
        label="Add photos"
        purpose="ProductImage"
        audience="staff"
        onUploaded={vi.fn()}
      />,
    );
    const input = getHiddenInput(container);
    await userEvent.upload(input, makeFile('huge.png', 'image/png', 6 * 1024 * 1024));

    expect(within(getRow()).getByText(/largest allowed here is 5 MB/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /retry/i })).not.toBeInTheDocument();
  });

  it('C3 — a server FileContentMismatch is shown with the §8.2 sentence, never the raw server text', async () => {
    const { container } = render(
      <FileUploader
        id="photos"
        label="Add photos"
        purpose="ProductImage"
        audience="staff"
        onUploaded={vi.fn()}
      />,
    );
    const input = getHiddenInput(container);
    await userEvent.upload(input, makeFile('mismatch-photo.png', 'image/png', 1024));

    const message = await within(getRow()).findByText(/does not look like a PNG file/);
    expect(message).not.toHaveTextContent(/does not match its declared type/);
  });

  it('C4 — FileScanUnavailable reads as a service state, keeps the row queued, and Retry succeeds', async () => {
    const onUploaded = vi.fn();
    const { container } = render(
      <FileUploader
        id="photos"
        label="Add photos"
        purpose="ProductImage"
        audience="staff"
        onUploaded={onUploaded}
      />,
    );
    const input = getHiddenInput(container);
    await userEvent.upload(input, makeFile('flaky-scan-once-card.png', 'image/png', 1024));

    await within(getRow()).findByText(/virus scanner is unavailable/);
    const retryButton = within(getRow()).getByRole('button', { name: /retry/i });
    await userEvent.click(retryButton);

    await waitFor(() => expect(onUploaded).toHaveBeenCalledTimes(1));
    expect(screen.queryByText(/virus scanner is unavailable/)).not.toBeInTheDocument();
  });

  it('C5 — Cancel mid-upload removes the row; releasing the (now-irrelevant) gate never calls onUploaded', async () => {
    armFileUploadGate('cu1');
    const onUploaded = vi.fn();
    const { container } = render(
      <FileUploader
        id="photos"
        label="Add photos"
        purpose="ProductImage"
        audience="staff"
        onUploaded={onUploaded}
      />,
    );
    const input = getHiddenInput(container);
    await userEvent.upload(input, makeFile('gate-cu1-photo.png', 'image/png', 1024));

    const cancelButton = await screen.findByRole('button', { name: /cancel gate-cu1-photo\.png/i });
    await userEvent.click(cancelButton);

    expect(screen.queryByText('gate-cu1-photo.png')).not.toBeInTheDocument();
    releaseFileUploadGate('cu1');
    await new Promise((resolve) => setTimeout(resolve, 0));
    expect(onUploaded).not.toHaveBeenCalled();
  });

  it('C6 — a rejected upload fires exactly one polite announcement naming the reason', async () => {
    const { container } = render(
      <FileUploader
        id="photos"
        label="Add photos"
        purpose="ProductImage"
        audience="staff"
        onUploaded={vi.fn()}
      />,
    );
    const input = getHiddenInput(container);
    expect(screen.getByRole('status')).toHaveTextContent('');

    await userEvent.upload(input, makeFile('reject-me.png', 'image/png', 1024));
    // The live region is re-keyed per announcement (ADR-FE-153) so a repeated message is still a
    // fresh node for assistive tech — re-query it fresh each time rather than holding a reference
    // to what may already be a detached node.
    await waitFor(() =>
      expect(screen.getByRole('status')).toHaveTextContent(/rejected by the virus scanner/),
    );
  });

  it('ADR-FE-153 — while uploading, the live region never carries a progress percentage', async () => {
    armFileUploadGate('prog1');
    const { container } = render(
      <FileUploader
        id="photos"
        label="Add photos"
        purpose="ProductImage"
        audience="staff"
        onUploaded={vi.fn()}
      />,
    );
    const input = getHiddenInput(container);
    await userEvent.upload(input, makeFile('gate-prog1-photo.png', 'image/png', 1024));

    await waitFor(() => expect(screen.getByRole('progressbar')).toBeInTheDocument());
    expect(screen.getByRole('status')).toHaveTextContent('');

    releaseFileUploadGate('prog1');
    await waitFor(() => expect(screen.getByRole('status')).toHaveTextContent(/finished uploading/));
  });

  it('disabled — the drop-zone control cannot be activated', () => {
    render(
      <FileUploader
        id="photos"
        label="Add photos"
        purpose="ProductImage"
        audience="staff"
        onUploaded={vi.fn()}
        disabled
      />,
    );
    expect(screen.getByRole('button', { name: /add photos/i })).toBeDisabled();
  });

  describe('FIL backlog 15 / 16 / 18', () => {
    const rateLimited = (retryAfter: string) => () => ({
      status: 429,
      body: {},
      headers: { 'Retry-After': retryAfter },
    });

    it('15 — each failed row has its own Retry button name', async () => {
      installFakeXhr({ respond: rateLimited('0') });
      const { container } = render(
        <FileUploader
          id="photos"
          label="Add photos"
          purpose="ProductImage"
          audience="staff"
          onUploaded={vi.fn()}
        />,
      );
      await userEvent.upload(getHiddenInput(container), [
        makeFile('one.png', 'image/png'),
        makeFile('two.png', 'image/png'),
      ]);
      expect(await screen.findByRole('button', { name: 'Retry one.png' })).toBeInTheDocument();
      expect(await screen.findByRole('button', { name: 'Retry two.png' })).toBeInTheDocument();
    });

    it('16 — multiple={false} keeps only the first of several dropped files', () => {
      render(
        <FileUploader
          id="photo"
          label="Add photo"
          purpose="ProductImage"
          audience="staff"
          multiple={false}
          onUploaded={vi.fn()}
        />,
      );
      const files = [
        makeFile('a.png', 'image/png'),
        makeFile('b.png', 'image/png'),
        makeFile('c.png', 'image/png'),
      ];
      fireEvent.drop(screen.getByRole('button', { name: /add photo/i }), {
        dataTransfer: { files },
      });
      const rows = screen.getAllByRole('listitem');
      expect(rows).toHaveLength(1);
      expect(rows[0]).toHaveTextContent('a.png');
    });

    it('16 — multiple (default) still accepts several dropped files', () => {
      render(
        <FileUploader
          id="photos"
          label="Add photos"
          purpose="ProductImage"
          audience="staff"
          onUploaded={vi.fn()}
        />,
      );
      const files = [makeFile('a.png', 'image/png'), makeFile('b.png', 'image/png')];
      fireEvent.drop(screen.getByRole('button', { name: /add photos/i }), {
        dataTransfer: { files },
      });
      expect(screen.getAllByRole('listitem')).toHaveLength(2);
    });

    it('18 — a 429 shows the Retry-After wait and keeps Retry disabled until it passes', async () => {
      installFakeXhr({ respond: rateLimited('2') });
      const { container } = render(
        <FileUploader
          id="photos"
          label="Add photos"
          purpose="ProductImage"
          audience="staff"
          onUploaded={vi.fn()}
        />,
      );
      await userEvent.upload(getHiddenInput(container), makeFile('busy.png', 'image/png'));

      expect(await within(getRow()).findByText(/too many uploads/i)).toBeInTheDocument();
      expect(within(getRow()).getByText(/you can retry in \d seconds?/i)).toBeInTheDocument();
      const retry = within(getRow()).getByRole('button', { name: 'Retry busy.png' });
      expect(retry).toBeDisabled();

      await waitFor(() => expect(retry).toBeEnabled(), { timeout: 4000 });
      expect(within(getRow()).queryByText(/you can retry in/i)).not.toBeInTheDocument();
    });
  });
});
