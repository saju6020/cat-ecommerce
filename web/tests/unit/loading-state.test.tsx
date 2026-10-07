import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { LoadingState } from '@/components/common/LoadingState';

describe('LoadingState', () => {
  it('announces loading once via a status live region, not per skeleton bar', () => {
    render(<LoadingState rows={4} />);
    const status = screen.getByRole('status');
    expect(status).toHaveTextContent('Loading…');
  });

  it('renders skeleton placeholders, not a spinner', () => {
    const { container } = render(<LoadingState rows={3} variant="table" />);
    expect(container.querySelectorAll('[aria-hidden="true"] > div')).toHaveLength(3);
  });

  it('accepts a custom label', () => {
    render(<LoadingState label="Loading cats…" />);
    expect(screen.getByRole('status')).toHaveTextContent('Loading cats…');
  });
});
