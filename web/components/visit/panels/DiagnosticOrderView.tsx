'use client';

import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { PermissionDeniedState } from '@/components/admin/PermissionDeniedState';
import { ErrorState } from '@/components/common/ErrorState';
import { LoadingState } from '@/components/common/LoadingState';
import { FileUploadField } from '@/components/files/FileUploadField';
import { AttachmentList } from './AttachmentList';
import { ResultEntryForm } from './ResultEntryForm';
import { ResultTable } from './ResultTable';
import { ApiForbiddenError, ApiNotFoundError } from '@/lib/api/client';
import { mapClinicalFormErrors } from '@/lib/api/errors';
import {
  useAttachDiagnosticFile,
  useDiagnosticOrder,
  useRecordResults,
} from '@/lib/hooks/useDiagnostics';
import { diagnosticKindLabel, diagnosticStatusLabel } from '@/lib/format/clinical';
import { formatInZone, todayInZone } from '@/lib/format/time';
import { MAX_RESULT_NOTE } from '@/lib/validation/clinical';
import type { DiagnosticOrder } from '@/lib/api/diagnostics';
import type { StoredFileViewModel } from '@/lib/api/files';

/**
 * ⭐ `DiagnosticOrderView` — HO-EMR-002 §11.3 (US-CLN-14). The content of both
 * `/staff/diagnostics/[orderId]` and `DiagnosticOrderDrawer` (ADR-FE-049 — over the workspace, never a
 * navigation): the order, its requested tests, the result sets (server flags, superseded sets
 * collapsed), the attachments (Open / Download from signed URLs), and — only for someone who may write
 * (the container decides, from `allowedActions`) — result entry and a file attach. Loading, error,
 * not-found and `403` are each their own state.
 */
export function DiagnosticOrderView({
  orderId,
  canWrite,
  zone,
}: {
  orderId: string;
  canWrite: boolean;
  zone: string;
}) {
  const query = useDiagnosticOrder(orderId);
  if (query.isLoading) return <LoadingState variant="card" rows={5} label="Loading the order…" />;
  if (query.error instanceof ApiForbiddenError) return <PermissionDeniedState />;
  if (query.error instanceof ApiNotFoundError) {
    return (
      <ErrorState
        title="We couldn’t find this order."
        description="It may belong to another branch."
      />
    );
  }
  if (query.isError || !query.data) return <ErrorState onRetry={() => void query.refetch()} />;
  return <OrderDetail order={query.data} canWrite={canWrite} zone={zone} />;
}

function OrderDetail({
  order,
  canWrite,
  zone,
}: {
  order: DiagnosticOrder;
  canWrite: boolean;
  zone: string;
}) {
  const record = useRecordResults(order.id, order.visitId, order.catId);
  const writable = canWrite && order.status !== 'Cancelled';
  return (
    <div className="flex flex-col gap-6">
      <section aria-labelledby="order-summary" className="flex flex-col gap-1 text-sm">
        <h2 id="order-summary" className="text-base font-semibold text-ink">
          {diagnosticKindLabel(order.kind)} for {order.catName}
        </h2>
        <p className="text-ink-muted">
          {diagnosticStatusLabel(order.status)} · ordered by {order.orderedBy} ·{' '}
          {formatInZone(order.orderedOn, zone)}
        </p>
        {order.description && <p className="whitespace-pre-wrap text-ink">{order.description}</p>}
        {order.cancelledReason && (
          <p className="text-ink-muted">Cancelled: {order.cancelledReason}</p>
        )}
        {order.requestedAnalytes.length > 0 && (
          <p className="text-ink">
            <span className="font-semibold">Tests asked for:</span>{' '}
            {order.requestedAnalytes.map((a) => a.name).join(', ')}
          </p>
        )}
      </section>

      <section aria-labelledby="order-results" className="flex flex-col gap-3">
        <h2 id="order-results" className="text-sm font-bold uppercase tracking-wide text-ink-muted">
          Results
        </h2>
        <ResultTable sets={order.resultSets} zone={zone} />
        {writable && (
          <details className="rounded-[var(--radius-sm)] border border-border p-3">
            <summary className="cursor-pointer text-sm font-semibold text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent">
              Enter results
            </summary>
            <div className="mt-3">
              <ResultEntryForm
                order={order}
                zone={zone}
                save={(request) => record.mutateAsync(request)}
              />
            </div>
          </details>
        )}
      </section>

      <section aria-labelledby="order-files" className="flex flex-col gap-3">
        <h2 id="order-files" className="text-sm font-bold uppercase tracking-wide text-ink-muted">
          Attachments
        </h2>
        <AttachmentList attachments={order.attachments} />
        {writable && <AttachForm order={order} zone={zone} />}
      </section>
    </div>
  );
}

function AttachForm({ order, zone }: { order: DiagnosticOrder; zone: string }) {
  const attach = useAttachDiagnosticFile(order.id, order.visitId, order.catId);
  const [file, setFile] = useState<StoredFileViewModel | null>(null);
  const [resultDate, setResultDate] = useState(() => todayInZone(zone));
  const [note, setNote] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);

  async function submit() {
    if (!file) {
      setError('Choose a file to attach first.');
      return;
    }
    setPending(true);
    setError(null);
    try {
      await attach.mutateAsync({
        fileId: file.fileId,
        resultDate,
        ...(note.trim() ? { note: note.trim() } : {}),
      });
      setFile(null);
      setNote('');
    } catch (caught) {
      setError(mapClinicalFormErrors(caught).root);
    } finally {
      setPending(false);
    }
  }

  return (
    <form
      noValidate
      aria-label="Attach a file"
      className="flex flex-col gap-3 rounded-[var(--radius-sm)] border border-border p-3"
      onSubmit={(event) => {
        event.preventDefault();
        void submit();
      }}
    >
      <FileUploadField
        id={`attach-${order.id}`}
        label="Attach a lab file"
        purpose="LabResult"
        value={file}
        onChange={setFile}
      />
      <div className="grid gap-3 min-[460px]:grid-cols-2">
        <div className="flex flex-col gap-1.5">
          <label htmlFor="attach-date" className="text-sm font-semibold text-ink">
            Result date
          </label>
          <input
            id="attach-date"
            type="date"
            max={todayInZone(zone)}
            value={resultDate}
            onChange={(event) => setResultDate(event.target.value)}
            className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
          />
        </div>
        <div className="flex flex-col gap-1.5">
          <label htmlFor="attach-note" className="text-sm font-semibold text-ink">
            Note <span className="font-normal text-ink-muted">(optional)</span>
          </label>
          <input
            id="attach-note"
            type="text"
            maxLength={MAX_RESULT_NOTE}
            value={note}
            onChange={(event) => setNote(event.target.value)}
            className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
          />
        </div>
      </div>
      {error && (
        <p role="alert" className="text-sm font-semibold text-danger-ink">
          {error}
        </p>
      )}
      <div>
        <Button type="submit" variant="secondary" disabled={pending} aria-busy={pending}>
          {pending ? 'Attaching…' : 'Attach file'}
        </Button>
      </div>
    </form>
  );
}
