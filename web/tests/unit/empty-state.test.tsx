import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import Link from 'next/link';
import { EmptyState, EmptyCatIllustration } from '@/components/common/EmptyState';

describe('EmptyState', () => {
  it('says what to do next, not just "No data"', () => {
    render(
      <EmptyState
        title="No cats registered yet."
        description="Add your first cat to start keeping their care in one place."
        // next/link, not a bare <a> — HO-CAT-002's /staff/cats/new is now a real route, so the
        // no-html-link-for-pages rule (correctly) flags a plain anchor pointed at it.
        action={<Link href="/staff/cats/new">Add a cat</Link>}
      />,
    );

    expect(screen.getByText('No cats registered yet.')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Add a cat' })).toBeInTheDocument();
    expect(screen.queryByText(/^no data$/i)).not.toBeInTheDocument();
  });

  it('is announced as a live region so screen readers hear it replace a loading/list state', () => {
    render(<EmptyState title="Nothing here yet." />);
    expect(screen.getByRole('status')).toHaveTextContent('Nothing here yet.');
  });

  it('defaults to a neutral icon, never the warm customer illustration, unless told otherwise', () => {
    const { container, rerender } = render(<EmptyState title="No results." />);
    expect(container.querySelector('svg path[d^="M19 23"]')).not.toBeInTheDocument();

    rerender(<EmptyState title="No cats yet." icon={<EmptyCatIllustration />} />);
    expect(container.querySelector('svg path[d^="M19 23"]')).toBeInTheDocument();
  });

  it('renders no icon at all when icon is explicitly null', () => {
    const { container } = render(<EmptyState title="No results." icon={null} />);
    expect(container.querySelector('svg')).not.toBeInTheDocument();
  });
});
