import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { ServiceTable } from '@/components/admin/ServiceTable';
import type { ClinicService } from '@/lib/api/admin-catalogue';

vi.mock('next/navigation', () => ({
  useRouter: () => ({ replace: vi.fn() }),
  usePathname: () => '/staff/admin/services',
  useSearchParams: () => new URLSearchParams(),
}));

const SERVICES: ClinicService[] = [
  {
    id: 's1',
    code: 'CONSULT',
    name: 'Consultation',
    description: '',
    category: 'Consultation',
    durationMinutes: 20,
    qualifiedRoleIds: ['Vet'],
    requiredRoomKind: 'Consultation',
    taxClassId: 't1',
    isBookableOnline: true,
    isActive: true,
    isPriced: true,
    currentPrice: { amount: '1500.00', currencyCode: 'BDT' },
  },
  {
    id: 's2',
    code: 'NEWSVC',
    name: 'New procedure',
    description: '',
    category: 'Emergency',
    durationMinutes: 30,
    qualifiedRoleIds: ['Vet'],
    requiredRoomKind: 'Theatre',
    taxClassId: 't1',
    isBookableOnline: false,
    isActive: true,
    isPriced: false,
    currentPrice: null,
  },
];

describe('ServiceTable — HO-ADM-002 §11.3, US-ADM-04', () => {
  it('U-C3: an unpriced service shows a "Not priced" badge with explanatory text', () => {
    render(
      <ServiceTable
        services={SERVICES}
        isLoading={false}
        isError={false}
        onRetry={() => {}}
        canManage
      />,
    );
    expect(screen.getByText('Not priced')).toBeInTheDocument();
    expect(screen.getByText(/1,500\.00|1500\.00/)).toBeInTheDocument();
  });

  it('hides "New service" when the caller cannot manage services', () => {
    render(
      <ServiceTable
        services={SERVICES}
        isLoading={false}
        isError={false}
        onRetry={() => {}}
        canManage={false}
      />,
    );
    expect(screen.queryByRole('link', { name: /new service/i })).not.toBeInTheDocument();
  });
});
