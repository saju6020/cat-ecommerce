'use client';

import { use, useState } from 'react';
import Link from 'next/link';
import { useCat } from '@/lib/hooks/useCat';
import {
  useFlagCatHighRisk,
  useClearCatHighRiskFlag,
  useMarkCatDeceased,
  useTransferCatOwnership,
} from '@/lib/hooks/useCatMutations';
import { Button, buttonVariants } from '@/components/ui/button';
import { HighRiskFlagDialog } from '@/components/cat/HighRiskFlagDialog';
import { MarkDeceasedDialog } from '@/components/cat/MarkDeceasedDialog';
import { TransferOwnershipDialog } from '@/components/cat/TransferOwnershipDialog';
import { toast } from '@/components/ui/use-toast';

/**
 * Cat overview — the landing tab under `[catId]/layout.tsx`. Hosts the destructive/audited
 * actions (US-CAT-04, US-CAT-08, US-CAT-09) as dialogs rather than separate routes, since each
 * one is a single confirm-and-submit step (UI-3), not a multi-field screen of its own.
 *
 * "Book appointment" — HO-CAT-002 §1's 2026-09-22 amendment: links to
 * `/staff/appointments/new?customerId=…&catId=…`, Appointments' own route (HO-APT-002). That
 * route doesn't exist in this branch yet; the link is real and will start working once it does.
 */
export default function CatOverviewPage({ params }: { params: Promise<{ catId: string }> }) {
  const { catId } = use(params);
  const catQuery = useCat(catId);
  const flagHighRisk = useFlagCatHighRisk(catId);
  const clearHighRisk = useClearCatHighRiskFlag(catId);
  const markDeceased = useMarkCatDeceased(catId);
  const transferOwnership = useTransferCatOwnership(catId);

  const [highRiskDialogOpen, setHighRiskDialogOpen] = useState(false);
  const [deceasedDialogOpen, setDeceasedDialogOpen] = useState(false);
  const [transferDialogOpen, setTransferDialogOpen] = useState(false);

  if (!catQuery.data) return null; // layout.tsx already renders loading/error/not-found
  const cat = catQuery.data;
  const isDeceased = cat.lifeStatus === 'Deceased';

  return (
    <div className="flex flex-col gap-6">
      <section className="grid gap-4 rounded-[var(--radius-md)] border border-border bg-surface p-6 sm:grid-cols-2">
        <div>
          <h2 className="text-xs font-semibold uppercase tracking-wide text-ink-muted">
            Dietary notes
          </h2>
          <p className="mt-1 text-sm text-ink">{cat.dietaryNotes || 'None recorded.'}</p>
        </div>
        <div>
          <h2 className="text-xs font-semibold uppercase tracking-wide text-ink-muted">
            Behavioural notes
          </h2>
          <p className="mt-1 text-sm text-ink">{cat.behaviouralNotes || 'None recorded.'}</p>
        </div>
      </section>

      <section className="flex flex-wrap gap-3">
        <Link
          href={`/staff/appointments/new?customerId=${cat.ownerId}&catId=${cat.id}`}
          className={buttonVariants({ variant: 'secondary' })}
        >
          Book appointment
        </Link>

        {cat.isHighRisk ? (
          <Button variant="secondary" onClick={() => setHighRiskDialogOpen(true)}>
            Clear high-risk flag
          </Button>
        ) : (
          <Button variant="secondary" onClick={() => setHighRiskDialogOpen(true)}>
            Flag as high-risk
          </Button>
        )}

        <Button
          variant="secondary"
          disabled={isDeceased}
          title={isDeceased ? 'This cat is already recorded as deceased.' : undefined}
          onClick={() => setTransferDialogOpen(true)}
        >
          Transfer ownership
        </Button>

        <Button
          variant="destructive"
          disabled={isDeceased}
          title={isDeceased ? 'This cat is already recorded as deceased.' : undefined}
          onClick={() => setDeceasedDialogOpen(true)}
        >
          Mark as deceased
        </Button>
      </section>

      <HighRiskFlagDialog
        mode={cat.isHighRisk ? 'clear' : 'flag'}
        catName={cat.name}
        open={highRiskDialogOpen}
        onOpenChange={setHighRiskDialogOpen}
        isSubmitting={flagHighRisk.isPending || clearHighRisk.isPending}
        onSubmit={async (values) => {
          if (values.mode === 'flag') {
            await flagHighRisk.mutateAsync({
              handlingNote: values.handlingNote,
              reason: values.reason,
            });
          } else {
            await clearHighRisk.mutateAsync({ reason: values.reason });
          }
          toast({ title: cat.isHighRisk ? 'High-risk flag cleared' : 'Flagged as high-risk' });
          setHighRiskDialogOpen(false);
        }}
      />

      <MarkDeceasedDialog
        catName={cat.name}
        open={deceasedDialogOpen}
        onOpenChange={setDeceasedDialogOpen}
        isSubmitting={markDeceased.isPending}
        onSubmit={async (values) => {
          await markDeceased.mutateAsync({
            deceasedOn: values.deceasedOn,
            note: values.note || null,
          });
          toast({ title: `${cat.name} marked as deceased` });
          setDeceasedDialogOpen(false);
        }}
      />

      <TransferOwnershipDialog
        catName={cat.name}
        open={transferDialogOpen}
        onOpenChange={setTransferDialogOpen}
        isSubmitting={transferOwnership.isPending}
        onSubmit={async (values) => {
          await transferOwnership.mutateAsync(values);
          toast({ title: 'Ownership transferred' });
          setTransferDialogOpen(false);
        }}
      />
    </div>
  );
}
