'use client';

import { useState } from 'react';
import Link from 'next/link';
import { Button } from '@/components/ui/button';
import { applyValidationErrors, describeUnexpectedError } from '@/lib/api/errors';
import { ApiValidationError } from '@/lib/api/client';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import type { PolicyDocument, CreatePolicyDocumentRequest } from '@/lib/api/admin-content';

const schema = z.object({
  key: z
    .string()
    .trim()
    .min(1, 'Enter a key.')
    .regex(/^[a-z0-9]+(-[a-z0-9]+)*$/, 'Lower-case letters, digits and single hyphens only.'),
  title: z.string().trim().min(1, 'Enter a title.'),
  requiresAcceptance: z.boolean(),
});
type FormValues = z.infer<typeof schema>;
const FIELD_MAP = { Key: 'key', Title: 'title', RequiresAcceptance: 'requiresAcceptance' } as const;

/**
 * `components/content/PolicyList.tsx` — HO-ADM-002 §11.6, US-ADM-07. Visible to a Shop Manager
 * too (`Content.Manage` guards the list), but "New policy" is shown only when `canManage` is
 * true — Administrator-only, owner decision 2026-09-26, HO-ADM-001 Amended (28): every policy
 * document's create AND publish is Administrator-only, for every key, `Policies.Manage`. The
 * server already refuses this call with a `403` regardless of this flag — this is presentation
 * only (ADR-043/044).
 */
export interface PolicyListProps {
  policies: PolicyDocument[] | undefined;
  canManage: boolean;
  onCreate: (request: CreatePolicyDocumentRequest) => Promise<unknown>;
  isPending: boolean;
}

export function PolicyList({ policies, canManage, onCreate, isPending }: PolicyListProps) {
  const [formOpen, setFormOpen] = useState(false);
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { key: '', title: '', requiresAcceptance: false },
  });

  async function onSubmit(values: FormValues) {
    try {
      await onCreate(values);
      setFormOpen(false);
    } catch (error) {
      if (error instanceof ApiValidationError) {
        applyValidationErrors(error, setError, { fieldMap: FIELD_MAP });
      } else {
        setError('root', { type: 'server', message: describeUnexpectedError(error) });
      }
    }
  }

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between gap-3">
        <h1 className="font-display text-xl font-semibold text-ink">Policies</h1>
        {canManage && (
          <Button type="button" variant="secondary" onClick={() => setFormOpen((v) => !v)}>
            New policy
          </Button>
        )}
      </div>

      {formOpen && canManage && (
        <form
          onSubmit={handleSubmit(onSubmit)}
          noValidate
          className="flex max-w-sm flex-col gap-3 rounded-[var(--radius-md)] border border-border p-4"
        >
          {errors.root && (
            <p role="alert" className="text-sm font-semibold text-danger-ink">
              {errors.root.message}
            </p>
          )}
          <div className="flex flex-col gap-1.5">
            <label htmlFor="policy-key" className="text-sm font-semibold text-ink">
              Key
            </label>
            <input
              id="policy-key"
              {...register('key')}
              className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
            />
            {errors.key && (
              <p role="alert" className="text-xs font-semibold text-danger-ink">
                {errors.key.message}
              </p>
            )}
          </div>
          <div className="flex flex-col gap-1.5">
            <label htmlFor="policy-title" className="text-sm font-semibold text-ink">
              Title
            </label>
            <input
              id="policy-title"
              {...register('title')}
              className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
            />
            {errors.title && (
              <p role="alert" className="text-xs font-semibold text-danger-ink">
                {errors.title.message}
              </p>
            )}
          </div>
          <label className="flex items-center gap-2 text-sm text-ink">
            <input type="checkbox" {...register('requiresAcceptance')} />
            Customers must accept new versions
          </label>
          <Button
            type="submit"
            disabled={isSubmitting || isPending}
            aria-busy={isSubmitting || isPending}
            className="w-fit"
          >
            {isSubmitting || isPending ? 'Creating…' : 'Create policy'}
          </Button>
        </form>
      )}

      <ul className="flex flex-col gap-2">
        {(policies ?? []).map((policy) => (
          <li key={policy.key}>
            <Link
              href={`/staff/admin/policies/${policy.key}`}
              className="block rounded-[var(--radius-md)] border border-border p-3 no-underline hover:bg-surface-sunken"
            >
              <p className="text-sm font-semibold text-ink">{policy.title}</p>
              <p className="text-sm text-ink-muted">
                {policy.key} · current version {policy.currentVersionNumber ?? 'none'}
              </p>
            </Link>
          </li>
        ))}
        {(!policies || policies.length === 0) && (
          <p className="text-sm text-ink-muted">No policy documents yet.</p>
        )}
      </ul>
    </div>
  );
}
