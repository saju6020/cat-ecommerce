import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ErrorState } from '@/components/common/ErrorState';

describe('ErrorState', () => {
  it('renders a plain-language message and a Try again action', async () => {
    const onRetry = vi.fn();
    render(<ErrorState onRetry={onRetry} />);

    expect(screen.getByRole('status')).toBeInTheDocument();
    const retry = screen.getByRole('button', { name: 'Try again' });
    await userEvent.click(retry);
    expect(onRetry).toHaveBeenCalledOnce();
  });

  it('renders no retry control when onRetry is omitted', () => {
    render(<ErrorState />);
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });

  it('never renders raw server text like an errorCode', () => {
    render(<ErrorState description="Something went wrong. Please try again." />);
    expect(screen.queryByText(/[A-Z][a-zA-Z]*(?:Error|Invalid|NotFound)/)).not.toBeInTheDocument();
  });

  it('ADM 2: headingLevel renders the title as a heading, and by default it stays a paragraph', () => {
    const { rerender } = render(<ErrorState title="Page not found" headingLevel={1} />);
    expect(screen.getByRole('heading', { level: 1, name: 'Page not found' })).toBeInTheDocument();
    rerender(<ErrorState title="Page not found" />);
    expect(screen.queryByRole('heading')).not.toBeInTheDocument();
  });
});
