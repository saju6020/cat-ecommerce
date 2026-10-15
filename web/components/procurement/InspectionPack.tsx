'use client';

import { Printer } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { DefinitionList, PageHeading, Panel } from '@/components/inventory/fields';
import { countryName } from '@/components/inventory/countries';
import { DOCUMENT_KIND_LABELS } from '@/components/inventory/labels';
import { StackedTable } from '@/components/inventory/StackedTable';
import { formatDateOrDash, formatQuantity } from '@/lib/format/inventory';
import type { InspectionPack as InspectionPackData } from '@/lib/api/procurement-consignments';
import { ConsignmentStatusBadge } from './StatusBadges';

/**
 * `components/procurement/InspectionPack.tsx` — HO-IMP-002 §11.2, US-IMP-03 AC2. One printable page:
 * the consignment's details, HS codes, permit / certificate / declaration numbers, purchase orders,
 * EVERY document (superseded ones included, and labelled) and every batch with lot and expiry.
 * `window.print()` with a print stylesheet that hides the app chrome — no PDF library.
 */
export function InspectionPack({ pack }: { pack: InspectionPackData }) {
  return (
    <div className="flex flex-col gap-5">
      <style>{`@media print { aside, header, nav, [data-no-print] { display: none !important; } main { padding: 0 !important; } }`}</style>
      <div data-no-print>
        <Button variant="secondary" onClick={() => window.print()}>
          <Printer size={14} aria-hidden="true" />
          Print this pack
        </Button>
      </div>
      <PageHeading
        title={`Inspection pack — ${pack.reference}`}
        description={
          <span className="flex flex-wrap items-center gap-2">
            <ConsignmentStatusBadge status={pack.status} />
            <span>
              {countryName(pack.originCountryCode)} · shipment {pack.shipmentReference}
            </span>
          </span>
        }
      />
      <Panel title="Consignment">
        <DefinitionList
          items={[
            { label: 'Expected arrival', value: formatDateOrDash(pack.expectedArrivalOn) },
            { label: 'Cleared on', value: formatDateOrDash(pack.clearedOn, 'Not cleared') },
            { label: 'Import permit number', value: pack.importPermitNumber ?? 'Not recorded' },
            { label: 'Health certificate number', value: pack.healthCertificateNumber ?? 'Not recorded' },
            { label: 'Customs declaration number', value: pack.customsDeclarationNumber ?? 'Not recorded' },
            {
              label: 'HS codes',
              value:
                pack.hsCodes.length === 0
                  ? 'None recorded'
                  : pack.hsCodes.map((h) => (h.description ? `${h.code} (${h.description})` : h.code)).join('; '),
            },
          ]}
        />
      </Panel>
      <Panel title="Purchase orders">
        <ul className="text-sm">
          {pack.purchaseOrders.map((po) => (
            <li key={po.id}>
              {po.number} — {po.supplierName}
            </li>
          ))}
        </ul>
      </Panel>
      <Panel title="Documents">
        {pack.documents.length === 0 ? (
          <p className="text-sm text-ink-muted">No documents recorded.</p>
        ) : (
          <StackedTable
            caption="Documents, including superseded ones"
            rows={pack.documents}
            getKey={(d) => d.documentId}
            columns={[
              { id: 'kind', header: 'Document', rowHeader: true, cell: (d) => DOCUMENT_KIND_LABELS[d.kind] ?? d.kind },
              { id: 'number', header: 'Number', cell: (d) => d.documentNumber ?? '—' },
              { id: 'issued', header: 'Issued', cell: (d) => formatDateOrDash(d.issuedOn) },
              { id: 'file', header: 'File', cell: (d) => d.fileName },
              { id: 'state', header: 'State', cell: (d) => (d.isSuperseded ? 'Superseded' : 'Current') },
            ]}
          />
        )}
      </Panel>
      <Panel title="Batches received">
        {pack.batches.length === 0 ? (
          <p className="text-sm text-ink-muted">Nothing has been received on this consignment yet.</p>
        ) : (
          <StackedTable
            caption="Batches received"
            rows={pack.batches}
            getKey={(b) => b.batchId}
            columns={[
              { id: 'item', header: 'Item', rowHeader: true, cell: (b) => `${b.displayName} (${b.skuCode})` },
              { id: 'lot', header: 'Lot', cell: (b) => b.lotNumber },
              { id: 'expiry', header: 'Expiry', cell: (b) => formatDateOrDash(b.expiresOn, 'No expiry date') },
              { id: 'qty', header: 'Received', numeric: true, cell: (b) => formatQuantity(b.receivedQuantity, b.stockUnit) },
              { id: 'receipt', header: 'Receipt', cell: (b) => b.goodsReceiptNumber },
            ]}
          />
        )}
      </Panel>
    </div>
  );
}
