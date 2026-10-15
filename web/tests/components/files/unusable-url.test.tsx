import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { FileList } from '@/components/files/FileList';
import { FileThumb } from '@/components/files/FileThumb';
import { MediaViewer } from '@/components/files/MediaViewer';
import { buildStoredFileViewModel } from '@/mocks/handlers/files/fixtures';

/** An empty `url` is `resolveStoredFile`'s "unusable" (FIL backlog 25): never an <img src>/<a href>. */
describe('components given an unusable (empty) file url', () => {
  const file = buildStoredFileViewModel({ url: '', originalFileName: 'x.png' });

  it('FileThumb shows the plain message and no <img>', () => {
    render(<FileThumb file={file} alt="x" />);
    expect(screen.queryByRole('img')).not.toBeInTheDocument();
    expect(screen.getByText(/could not be loaded/)).toBeInTheDocument();
  });

  it('FileList renders no download link', () => {
    render(<FileList files={[file]} />);
    expect(screen.getByText('x.png')).toBeInTheDocument();
    expect(screen.queryByRole('link')).not.toBeInTheDocument();
  });

  it('MediaViewer shows the plain message and no media element', () => {
    render(<MediaViewer file={file} open onOpenChange={() => {}} caption="x" />);
    expect(screen.getByText(/could not be loaded/)).toBeInTheDocument();
    expect(document.querySelector('img, iframe, video')).toBeNull();
  });
});
