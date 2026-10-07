import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { PhotoField } from '@/components/cat/PhotoField';

function makeFile(name: string, type: string, sizeInBytes: number): File {
  const file = new File([new ArrayBuffer(1)], name, { type });
  Object.defineProperty(file, 'size', { value: sizeInBytes });
  return file;
}

describe('PhotoField', () => {
  it('U14 — an 8 MB file is rejected, message names the 5 MB limit (US-CAT-01 AC3)', async () => {
    const onFileValidated = vi.fn();
    render(<PhotoField id="photo" onFileValidated={onFileValidated} />);

    const input = screen.getByLabelText('Photo');
    const file = makeFile('big.jpg', 'image/jpeg', 8.2 * 1024 * 1024);
    await userEvent.upload(input, file);

    expect(screen.getByRole('alert')).toHaveTextContent('5 MB');
    expect(onFileValidated).not.toHaveBeenCalled();
  });

  it('U15 — a disallowed type (image/gif) is rejected, message names the accepted types', async () => {
    const onFileValidated = vi.fn();
    render(<PhotoField id="photo" onFileValidated={onFileValidated} />);

    const input = screen.getByLabelText('Photo');
    const file = makeFile('animated.gif', 'image/gif', 1024);
    // The input's `accept` attribute is JPEG/PNG/WebP only, which user-event's default upload()
    // config filters against — a real browser's "All files" picker option can still bypass
    // `accept`, which is exactly why the component validates again in JS rather than relying on
    // it. `applyAccept: false` reproduces that bypass so this test exercises that JS check.
    await userEvent.setup({ applyAccept: false }).upload(input, file);

    const alert = screen.getByRole('alert');
    expect(alert).toHaveTextContent(/JPEG/i);
    expect(alert).toHaveTextContent(/PNG/i);
    expect(alert).toHaveTextContent(/WebP/i);
    expect(onFileValidated).not.toHaveBeenCalled();
  });

  it('U16 — a valid 2 MB JPEG is accepted', async () => {
    const onFileValidated = vi.fn();
    render(<PhotoField id="photo" onFileValidated={onFileValidated} />);

    const input = screen.getByLabelText('Photo');
    const file = makeFile('cat.jpg', 'image/jpeg', 2 * 1024 * 1024);
    await userEvent.upload(input, file);

    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expect(onFileValidated).toHaveBeenCalledWith(file);
  });
});
