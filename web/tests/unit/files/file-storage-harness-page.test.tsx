import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';

/**
 * Gate-fix 2026-09-28 (ARCHITECTURE BLOCKER / SECURITY MEDIUM, owner decision 2026-09-28) —
 * `/dev/file-storage-harness` must be unreachable in a production build: `notFound()` is called
 * during render, not from an effect, so this proves it fires for every render when
 * `NODE_ENV === 'production'`, with nothing else rendered — and that it does NOT fire outside
 * production, where the real harness content still mounts.
 */
const notFoundMock = vi.fn();
vi.mock('next/navigation', () => ({
  notFound: () => notFoundMock(),
}));

// Imported after the mock above so the page's own `import { notFound } from 'next/navigation'`
// resolves to it.
import FileStorageHarnessPage from '@/app/dev/file-storage-harness/page';

describe('FileStorageHarnessPage — production gate', () => {
  afterEach(() => {
    vi.unstubAllEnvs();
    notFoundMock.mockClear();
  });

  it('calls notFound() during render and renders nothing when NODE_ENV is production', () => {
    vi.stubEnv('NODE_ENV', 'production');

    const { container } = render(<FileStorageHarnessPage />);

    expect(notFoundMock).toHaveBeenCalledTimes(1);
    expect(container).toBeEmptyDOMElement();
    expect(screen.queryByRole('button', { name: /add product photos/i })).not.toBeInTheDocument();
  });

  it('renders the real harness content, without calling notFound(), outside production', async () => {
    vi.stubEnv('NODE_ENV', 'test');

    render(<FileStorageHarnessPage />);

    expect(
      await screen.findByRole(
        'heading',
        { name: /file storage — test harness/i },
        { timeout: 5000 },
      ),
    ).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /add product photos/i })).toBeInTheDocument();
    expect(notFoundMock).not.toHaveBeenCalled();
  });
});
