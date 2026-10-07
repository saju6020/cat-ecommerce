import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { FileList } from '@/components/files/FileList';
import { buildStoredFileViewModel } from '@/mocks/handlers/files/fixtures';

describe('<FileList> — HO-FIL-002 §3', () => {
  it('renders nothing by default when empty, or the supplied emptyMessage', () => {
    const { rerender } = render(<FileList files={[]} />);
    expect(document.querySelector('ul')).not.toBeInTheDocument();

    rerender(<FileList files={[]} emptyMessage="No files yet." />);
    expect(screen.getByText('No files yet.')).toBeInTheDocument();
  });

  it('shows name, a human type label, size and a working download link per file', () => {
    const file = buildStoredFileViewModel({ originalFileName: 'invoice.png', sizeInBytes: 2048 });
    render(<FileList files={[file]} />);

    expect(screen.getByText('invoice.png')).toBeInTheDocument();
    expect(screen.getByText(/PNG · 2 KB/)).toBeInTheDocument();
    const downloadLink = screen.getByRole('link', { name: /download invoice\.png/i });
    expect(downloadLink).toHaveAttribute('href', file.url);
    expect(downloadLink).toHaveAttribute('download', 'invoice.png');
  });

  it('calls onRemove with the file when its Remove button is clicked, and hides it otherwise', async () => {
    const file = buildStoredFileViewModel({ originalFileName: 'a.png' });
    const onRemove = vi.fn();
    const { rerender } = render(<FileList files={[file]} onRemove={onRemove} />);

    await userEvent.click(screen.getByRole('button', { name: /remove a\.png/i }));
    expect(onRemove).toHaveBeenCalledWith(file);

    rerender(<FileList files={[file]} />);
    expect(screen.queryByRole('button', { name: /remove a\.png/i })).not.toBeInTheDocument();
  });

  it('calls onView with the file when its thumb is activated', async () => {
    const file = buildStoredFileViewModel({ originalFileName: 'a.png' });
    const onView = vi.fn();
    render(<FileList files={[file]} onView={onView} getCaption={() => 'Photo of Whiskers'} />);

    await userEvent.click(screen.getByRole('button', { name: 'Photo of Whiskers' }));
    expect(onView).toHaveBeenCalledWith(file);
  });
});
