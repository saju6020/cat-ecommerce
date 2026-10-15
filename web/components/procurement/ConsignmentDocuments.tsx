'use client';

import * as React from 'react';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Banner, Panel, SelectField, TextField } from '@/components/inventory/fields';
import { DOCUMENT_KIND_LABELS } from '@/components/inventory/labels';
import {
  EMPTY_ERRORS,
  distributeViolations,
  focusErrorTarget,
  type DistributedErrors,
} from '@/components/inventory/lineErrors';
import { StackedTable } from '@/components/inventory/StackedTable';
import { ApiValidationError } from '@/lib/api/client';
import { describeUnexpectedError } from '@/lib/api/errors';
import { formatBytes, formatDateOrDash } from '@/lib/format/inventory';
import { formatInZone } from '@/lib/format/time';
import type {
  ConsignmentDocument,
  ConsignmentDocumentInput,
  ImportDocumentKind,
} from '@/lib/api/procurement-consignments';

export const DOCUMENT_CONTENT_TYPES = [
  { value: 'application/pdf', label: 'PDF' },
  { value: 'image/jpeg', label: 'JPEG image' },
  { value: 'image/png', label: 'PNG image' },
] as const;

const FIELDS = ['kind', 'documentNumber', 'issuedOn', 'fileName', 'contentType', 'sizeInBytes', 'storageKey'] as const;

interface FormState {
  kind: ImportDocumentKind | '';
  documentNumber: string;
  issuedOn: string;
  fileName: string;
  contentType: string;
  sizeInBytes: string;
  storageKey: string;
}

const EMPTY: FormState = {
  kind: '', documentNumber: '', issuedOn: '', fileName: '', contentType: 'application/pdf', sizeInBytes: '', storageKey: '',
};

/**
 * `components/procurement/ConsignmentDocuments.tsx` — HO-IMP-002 §11.2, US-IMP-03, NFR-LEG-2.
 * Every document is listed with its number and date, a superseded one stays visible and says so,
 * and there is NO delete control anywhere. **File upload is not available** (object storage is not
 * provisioned): the form records the document's details and where it is stored, honestly — and has
 * no file input.
 */
export function ConsignmentDocuments({
  documents,
  canAdd,
  timeZoneId,
  onAdd,
  onSupersede,
}: {
  documents: ConsignmentDocument[];
  canAdd: boolean;
  timeZoneId: string;
  onAdd: (request: ConsignmentDocumentInput) => Promise<unknown>;
  onSupersede: (documentId: string, request: ConsignmentDocumentInput) => Promise<unknown>;
}) {
  const [form, setForm] = React.useState<FormState>(EMPTY);
  const [replacing, setReplacing] = React.useState<ConsignmentDocument | null>(null);
  const [errors, setErrors] = React.useState<DistributedErrors>(EMPTY_ERRORS);
  const [saved, setSaved] = React.useState(false);
  const [pending, setPending] = React.useState(false);

  function patch(next: Partial<FormState>) {
    setForm((current) => ({ ...current, ...next }));
    setSaved(false);
  }

  function startReplace(document: ConsignmentDocument) {
    setReplacing(document);
    setForm({ ...EMPTY, kind: document.kind });
    setErrors(EMPTY_ERRORS);
    focusErrorTarget('doc', 'kind');
  }

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    const local: DistributedErrors = { fields: {}, lines: {}, general: [], firstTarget: null };
    const need = (field: keyof FormState, message: string) => {
      if (String(form[field]).trim() === '') {
        local.fields[field] = message;
        local.firstTarget ??= field;
      }
    };
    need('kind', 'Choose the kind of document.');
    need('fileName', 'Enter the file name.');
    need('storageKey', 'Enter where the file is stored.');
    if (!/^\d+$/.test(form.sizeInBytes.trim()) || form.sizeInBytes.trim() === '0') {
      local.fields.sizeInBytes = 'Enter the size in bytes, as a whole number.';
      local.firstTarget ??= 'sizeInBytes';
    }
    if (local.firstTarget) {
      setErrors(local);
      focusErrorTarget('doc', local.firstTarget);
      return;
    }
    setErrors(EMPTY_ERRORS);
    setPending(true);
    const request: ConsignmentDocumentInput = {
      kind: form.kind as ImportDocumentKind,
      documentNumber: form.documentNumber.trim() === '' ? null : form.documentNumber.trim(),
      issuedOn: form.issuedOn === '' ? null : form.issuedOn,
      fileName: form.fileName.trim(),
      storageKey: form.storageKey.trim(),
      contentType: form.contentType,
      sizeInBytes: parseInt(form.sizeInBytes.trim(), 10),
    };
    try {
      if (replacing) await onSupersede(replacing.documentId, request);
      else await onAdd(request);
      setForm(EMPTY);
      setReplacing(null);
      setSaved(true);
    } catch (error) {
      const distributed =
        error instanceof ApiValidationError
          ? distributeViolations(error, { knownFields: FIELDS })
          : { ...EMPTY_ERRORS, general: [describeUnexpectedError(error)] };
      setErrors(distributed);
      focusErrorTarget('doc', distributed.firstTarget);
    } finally {
      setPending(false);
    }
  }

  return (
    <div className="flex flex-col gap-5">
      <Panel title="Documents">
        {documents.length === 0 ? (
          <p className="text-sm text-ink-muted">No documents have been recorded for this consignment yet.</p>
        ) : (
          <StackedTable
            caption="Consignment documents"
            columns={[
              {
                id: 'kind',
                header: 'Document',
                rowHeader: true,
                cell: (d: ConsignmentDocument) => DOCUMENT_KIND_LABELS[d.kind] ?? d.kind,
              },
              { id: 'number', header: 'Number', cell: (d) => d.documentNumber ?? '—' },
              { id: 'issued', header: 'Issued', cell: (d) => formatDateOrDash(d.issuedOn) },
              { id: 'file', header: 'File', cell: (d) => `${d.fileName} (${formatBytes(d.sizeInBytes)})` },
              { id: 'uploaded', header: 'Recorded', cell: (d) => formatInZone(d.uploadedOn, timeZoneId) },
              {
                id: 'state',
                header: 'State',
                cell: (d) =>
                  d.isSuperseded ? (
                    <Badge variant="neutral" className="max-w-full whitespace-normal">Superseded{d.supersededOn ? ` ${formatInZone(d.supersededOn, timeZoneId)}` : ''}</Badge>
                  ) : (
                    <span className="flex flex-wrap items-center gap-2">
                      <Badge variant="success">Current</Badge>
                      {canAdd && (
                        <Button variant="ghost" size="sm" onClick={() => startReplace(d)} aria-label={`Replace ${DOCUMENT_KIND_LABELS[d.kind]} ${d.documentNumber ?? ''}`.trim()}>
                          Replace
                        </Button>
                      )}
                    </span>
                  ),
              },
            ]}
            rows={documents}
            getKey={(d) => d.documentId}
            rowClassName={(d) => (d.isSuperseded ? 'text-ink-muted' : undefined)}
          />
        )}
        <p className="mt-2 text-xs text-ink-muted">Documents are permanent records: they can be replaced, never deleted.</p>
      </Panel>

      {canAdd && (
        <Panel title={replacing ? `Replace ${DOCUMENT_KIND_LABELS[replacing.kind]}` : 'Add a document'}>
          <form onSubmit={submit} noValidate className="flex max-w-3xl flex-col gap-4">
            <Banner tone="neutral">
              File upload is not available yet — record the document&rsquo;s details and where it is stored.
            </Banner>
            {errors.general.length > 0 && (
              <Banner tone="danger">{errors.general.join(' ')}</Banner>
            )}
            <div className="grid gap-4 sm:grid-cols-2">
              <SelectField id="doc-kind" label="Kind" required value={form.kind} disabled={!!replacing} error={errors.fields.kind} onChange={(e) => patch({ kind: e.target.value as ImportDocumentKind })}>
                <option value="">Choose a kind</option>
                {Object.entries(DOCUMENT_KIND_LABELS).map(([value, label]) => (<option key={value} value={value}>{label}</option>))}
              </SelectField>
              <TextField id="doc-documentNumber" label="Document number" value={form.documentNumber} error={errors.fields.documentNumber} onChange={(e) => patch({ documentNumber: e.target.value })} />
              <TextField id="doc-issuedOn" label="Issued on" type="date" value={form.issuedOn} error={errors.fields.issuedOn} onChange={(e) => patch({ issuedOn: e.target.value })} />
              <TextField id="doc-fileName" label="File name" required value={form.fileName} error={errors.fields.fileName} onChange={(e) => patch({ fileName: e.target.value })} />
              <SelectField id="doc-contentType" label="File type" required value={form.contentType} error={errors.fields.contentType} onChange={(e) => patch({ contentType: e.target.value })}>
                {DOCUMENT_CONTENT_TYPES.map((t) => (<option key={t.value} value={t.value}>{t.label}</option>))}
              </SelectField>
              <TextField id="doc-sizeInBytes" label="Size (bytes)" required inputMode="numeric" value={form.sizeInBytes} hint="At most 20 MB." error={errors.fields.sizeInBytes} onChange={(e) => patch({ sizeInBytes: e.target.value })} />
              <TextField id="doc-storageKey" label="Stored at" required wrapperClassName="sm:col-span-2" value={form.storageKey} hint="The storage key or path where the file is kept." error={errors.fields.storageKey} onChange={(e) => patch({ storageKey: e.target.value })} />
            </div>
            {saved && <Banner tone="info">Recorded.</Banner>}
            <div className="flex flex-wrap gap-2">
              <Button type="submit" disabled={pending} aria-busy={pending}>
                {pending ? 'Saving…' : replacing ? 'Record replacement' : 'Record document'}
              </Button>
              {replacing && (
                <Button variant="secondary" onClick={() => { setReplacing(null); setForm(EMPTY); }}>
                  Cancel replacement
                </Button>
              )}
            </div>
          </form>
        </Panel>
      )}
    </div>
  );
}
