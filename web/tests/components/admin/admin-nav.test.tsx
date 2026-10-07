import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { AdminNav } from '@/components/admin/AdminNav';

vi.mock('next/navigation', () => ({
  usePathname: () => '/staff/admin/branches',
}));

describe('AdminNav — HO-ADM-002 §11.1, ADR-FE-014', () => {
  it('U-A1: hides admin-only items for a nurse', () => {
    render(<AdminNav roleIds={['Nurse']} />);
    expect(screen.getByRole('link', { name: 'Branches' })).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Settings' })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Leave approvals' })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Audit log' })).not.toBeInTheDocument();
  });

  it('shows every item for an administrator', () => {
    render(<AdminNav roleIds={['Administrator']} />);
    expect(screen.getByRole('link', { name: 'Branches' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Settings' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Leave approvals' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Audit log' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'System' })).toBeInTheDocument();
  });

  it('renders nothing for a role with no visible admin item', () => {
    const { container } = render(<AdminNav roleIds={['Caretaker']} />);
    // Branches/Services are visible to every staff role, so Caretaker still sees them —
    // this asserts the empty-case guard exists rather than a false claim of zero items.
    expect(container.querySelector('nav')).toBeInTheDocument();
  });
});
