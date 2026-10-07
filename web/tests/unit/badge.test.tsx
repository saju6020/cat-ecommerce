import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { Badge } from '@/components/ui/badge';

describe('Badge', () => {
  it('renders an icon alongside its text — colour is never the only signal (UI-6)', () => {
    render(
      <Badge variant="danger" icon={<svg data-testid="icon" aria-hidden="true" />}>
        Overdue
      </Badge>,
    );
    expect(screen.getByText('Overdue')).toBeInTheDocument();
    expect(screen.getByTestId('icon')).toBeInTheDocument();
  });

  it('defaults to the neutral variant', () => {
    render(<Badge>Draft</Badge>);
    expect(screen.getByText('Draft')).toBeInTheDocument();
  });
});
