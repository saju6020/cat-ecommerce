'use client';

import { useRef, useState } from 'react';
import { Download, Upload } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { ApiValidationError } from '@/lib/api/client';
import { describeBillingError } from '@/lib/api/errors';
import type { ImportStatementRequest } from '@/lib/api/billing-reconciliation';
import { looksLikeCardNumber, CARD_NUMBER_MESSAGE } from '@/lib/validation/cardNumberGuard';
import { FormRootError, TextField } from './Fields';

/**
 * `components/billing/StatementUpload.tsx` — HO-BIL-002 §11.8, NFR-SEC-5 (US-BIL-06 AC2). Import a bank
 * statement: the account label, the period and **one `.csv` of at most 1 MiB** — checked here by
 * extension and size before anything is sent (the server re-checks the content). The file is read once
 * by the browser, sent once and **never kept**: the input is cleared after every attempt. The canonical
 * CSV template is a link. A malformed file is answered with its **row numbers** (never their content),
 * listed here; an already-imported file is explained.
 */
export const MAX_STATEMENT_BYTES = 1_048_576; // 1 MiB

export function validateStatementFile(file: { name: string; size: number } | undefined): string | null {
  if (!file) return 'Choose the statement file (a .csv).';
  if (!/\.csv$/i.test(file.name)) return 'The statement must be a .csv file.';
  if (file.size > MAX_STATEMENT_BYTES) return 'The statement file is larger than 1 MiB. Split it by period and import each part.';
  return null;
}

export function StatementUpload({
  onImport,
  onImported,
}: {
  onImport: (request: ImportStatementRequest) => Promise<{ statementId: string; lineCount: number }>;
  onImported: (statementId: string) => void;
}) {
  const fileRef = useRef<HTMLInputElement>(null);
  const [label, setLabel] = useState('');
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const [fileError, setFileError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<{ label?: string; from?: string; to?: string }>({});
  const [rowErrors, setRowErrors] = useState<string[]>([]);
  const [formError, setFormError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  function checkFile() {
    setFileError(validateStatementFile(fileRef.current?.files?.[0]));
  }

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    const file = fileRef.current?.files?.[0];
    const errors: typeof fieldErrors = {};
    if (!label.trim()) errors.label = 'Enter the bank account label.';
    else if (looksLikeCardNumber(label)) errors.label = CARD_NUMBER_MESSAGE;
    if (!/^\d{4}-\d{2}-\d{2}$/.test(from)) errors.from = 'Enter the first day the statement covers.';
    if (!/^\d{4}-\d{2}-\d{2}$/.test(to)) errors.to = 'Enter the last day the statement covers.';
    const fileProblem = validateStatementFile(file);
    setFieldErrors(errors);
    setFileError(fileProblem);
    setRowErrors([]);
    setFormError(null);
    if (Object.keys(errors).length > 0 || fileProblem || !file) return;

    setBusy(true);
    try {
      const result = await onImport({ file, bankAccountLabel: label.trim(), periodFrom: from, periodTo: to });
      if (fileRef.current) fileRef.current.value = '';
      onImported(result.statementId);
    } catch (failure) {
      if (failure instanceof ApiValidationError && failure.response.errors.some((e) => e.errorCode === 'StatementMalformed' && /^Row \d+/.test(e.errorMessage))) {
        setRowErrors(failure.response.errors.map((e) => e.errorMessage));
      } else {
        setFormError(describeBillingError(failure));
      }
      if (fileRef.current) fileRef.current.value = ''; // never kept: choose the corrected file again
    } finally {
      setBusy(false);
    }
  }

  return (
    <form onSubmit={submit} noValidate className="flex max-w-2xl flex-col gap-4" aria-label="Import a bank statement">
      <FormRootError message={formError ?? undefined} />
      <TextField id="stmt-label" label="Bank account" required value={label} onChange={(e) => setLabel(e.target.value)} error={fieldErrors.label} />
      <div className="grid gap-4 sm:grid-cols-2">
        <TextField id="stmt-from" label="Period from" type="date" required value={from} onChange={(e) => setFrom(e.target.value)} error={fieldErrors.from} />
        <TextField id="stmt-to" label="Period to" type="date" required value={to} onChange={(e) => setTo(e.target.value)} error={fieldErrors.to} />
      </div>
      <div className="flex flex-col gap-1.5">
        <label htmlFor="stmt-file" className="text-sm font-semibold text-ink">
          Statement file (.csv, at most 1 MiB){' '}
          <span aria-hidden="true" className="text-danger-ink">*</span>
        </label>
        <input
          ref={fileRef}
          id="stmt-file"
          type="file"
          accept=".csv"
          aria-invalid={!!fileError}
          aria-describedby={fileError ? 'stmt-file-error' : 'stmt-file-hint'}
          onChange={checkFile}
          className="text-sm"
        />
        <p id="stmt-file-hint" className="text-xs text-ink-muted">
          Columns: date, amount, direction (C or D), reference, description, bankTransactionId.{' '}
          <a href="/templates/bank-statement-template.csv" download className="font-semibold text-accent-ink underline">
            <Download size={12} className="mr-1 inline" aria-hidden="true" />
            Download the CSV template
          </a>
        </p>
        {fileError && (
          <p id="stmt-file-error" role="alert" className="text-xs font-semibold text-danger-ink">{fileError}</p>
        )}
      </div>
      {rowErrors.length > 0 && (
        <div role="alert" className="rounded-[var(--radius-sm)] border border-border bg-danger-bg p-3 text-sm text-danger-ink">
          <p className="font-semibold">The file could not be read. Fix these rows and choose it again:</p>
          <ul className="mt-1 list-disc pl-5">
            {rowErrors.map((message) => (
              <li key={message}>{message}</li>
            ))}
          </ul>
        </div>
      )}
      <div>
        <Button type="submit" disabled={busy || !!fileError} aria-busy={busy}>
          <Upload size={14} aria-hidden="true" />
          {busy ? 'Importing…' : 'Import statement'}
        </Button>
      </div>
    </form>
  );
}
