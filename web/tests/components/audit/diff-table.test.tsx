import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { DiffTable } from '@/components/audit/DiffTable';

describe('DiffTable — HO-ADM-002 §11.7, US-ADM-06', () => {
  it('U-G2: a formula-looking value renders as plain text, never parsed into markup', () => {
    render(
      <DiffTable
        before={JSON.stringify({ note: 'ordinary' })}
        after={JSON.stringify({ note: '=HYPERLINK("https://evil.example","click")' })}
      />,
    );
    expect(screen.getByText('=HYPERLINK("https://evil.example","click")')).toBeInTheDocument();
    expect(screen.queryByRole('link')).not.toBeInTheDocument();
  });

  it('shows a key-by-key diff for every field present in either side', () => {
    render(
      <DiffTable
        before={JSON.stringify({ amount: '1500.00' })}
        after={JSON.stringify({ amount: '1800.00' })}
      />,
    );
    expect(screen.getByText('amount')).toBeInTheDocument();
    expect(screen.getByText('1500.00')).toBeInTheDocument();
    expect(screen.getByText('1800.00')).toBeInTheDocument();
  });

  it('handles no before/after value gracefully', () => {
    render(<DiffTable before={null} after={null} />);
    expect(screen.getByText(/no before\/after value recorded/i)).toBeInTheDocument();
  });
});
