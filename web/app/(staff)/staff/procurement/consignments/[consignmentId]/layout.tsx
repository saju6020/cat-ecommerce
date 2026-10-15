'use client';

import { use } from 'react';
import { PageHeading } from '@/components/inventory/fields';
import { QueryBoundary } from '@/components/inventory/QueryBoundary';
import { ConsignmentTabs } from '@/components/procurement/ConsignmentTabs';
import { ConsignmentStatusBadge } from '@/components/procurement/StatusBadges';
import { countryName } from '@/components/inventory/countries';
import { useConsignment } from '@/lib/hooks/useProcurement';

/** `app/(staff)/staff/procurement/consignments/[consignmentId]/layout.tsx` — the consignment's
 *  header and its tab row; each tab is a route. */
export default function ConsignmentLayout({
  children,
  params,
}: {
  children: React.ReactNode;
  params: Promise<{ consignmentId: string }>;
}) {
  const { consignmentId } = use(params);
  const query = useConsignment(consignmentId);
  return (
    <div className="flex flex-col gap-5">
      <QueryBoundary
        query={query}
        notFoundTitle="This consignment could not be found"
        notFoundDescription="It may belong to another branch."
      >
        {(c) => (
          <>
            <PageHeading
              title={c.reference}
              description={
                <span className="flex flex-wrap items-center gap-2">
                  <ConsignmentStatusBadge status={c.status} />
                  <span>
                    {countryName(c.originCountryCode)} · shipment {c.shipmentReference}
                  </span>
                </span>
              }
            />
            <ConsignmentTabs consignmentId={c.id} />
            <div className="min-w-0">{children}</div>
          </>
        )}
      </QueryBoundary>
    </div>
  );
}
