'use client';

import { AccessRefusedState } from '@/components/admin/AccessRefusedState';
import { PageHeader } from '@/components/billing/PageHeader';
import { QueryBoundary } from '@/components/billing/QueryBoundary';
import { ErrorState } from '@/components/common/ErrorState';
import { ApiForbiddenError } from '@/lib/api/client';
import { useDeliveryZone, useDiscountCodes } from '@/lib/hooks/useOrderingAdmin';
import { DiscountCodeForm } from './DiscountCodeForm';
import { ZoneForm } from './ZoneForm';

/** The new / edit wrappers of the two administration forms (HO-ORD-002 §11.12): each loads, then mounts the form. */
export function NewZoneScreen() {
  return (
    <div className="flex flex-col gap-5">
      <PageHeader back={{ href: '/staff/admin/delivery-zones', label: 'Delivery zones' }} title="New delivery zone" />
      <ZoneForm zone={null} />
    </div>
  );
}

export function EditZoneScreen({ zoneId }: { zoneId: string }) {
  const query = useDeliveryZone(zoneId);
  return (
    <div className="flex flex-col gap-5">
      <PageHeader back={{ href: '/staff/admin/delivery-zones', label: 'Delivery zones' }} title="Edit delivery zone" />
      <QueryBoundary query={query} notFoundTitle="Zone not found" loadingLabel="Loading the zone…">
        {(zone) => <ZoneForm key={`${zone.id}-${zone.version}`} zone={zone} />}
      </QueryBoundary>
    </div>
  );
}

export function NewCodeScreen() {
  return (
    <div className="flex flex-col gap-5">
      <PageHeader back={{ href: '/staff/admin/discount-codes', label: 'Discount codes' }} title="New discount code" />
      <DiscountCodeForm code={null} />
    </div>
  );
}

export function EditCodeScreen({ codeId }: { codeId: string }) {
  const query = useDiscountCodes();
  if (query.error instanceof ApiForbiddenError) return <AccessRefusedState error={query.error} />;
  return (
    <div className="flex flex-col gap-5">
      <PageHeader back={{ href: '/staff/admin/discount-codes', label: 'Discount codes' }} title="Edit discount code" />
      <QueryBoundary query={query} loadingLabel="Loading the code…">
        {(codes) => {
          const code = codes.find((c) => c.id === codeId);
          return code ? <DiscountCodeForm key={`${code.id}-${code.version}`} code={code} /> : <ErrorState title="Code not found" description="This discount code may have been removed." headingLevel={2} />;
        }}
      </QueryBoundary>
    </div>
  );
}
