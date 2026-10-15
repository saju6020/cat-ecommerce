'use client';

import { useState } from 'react';
import { Activity, Flag, Pill } from 'lucide-react';
import { ErrorState } from '@/components/common/ErrorState';
import { LoadingState } from '@/components/common/LoadingState';
import { Button } from '@/components/ui/button';
import { Banner } from '@/components/inventory/fields';
import { ApiForbiddenError } from '@/lib/api/client';
import { AccessRefusedState } from '@/components/admin/AccessRefusedState';
import { useAnaesthesiaLog } from '@/lib/hooks/useAnaesthesiaLog';
import { useStaffSession } from '@/lib/hooks/useSession';
import { useStaffRoles } from '@/lib/hooks/useStaffRoles';
import type { AnaesthesiaEntry } from '@/lib/api/anaesthesia-log';
import type { SurgeryDetail } from '@/lib/api/surgeries';
import { AnaesthesiaTimeline } from './AnaesthesiaTimeline';
import { CorrectEntryDialog } from './CorrectEntryDialog';
import { DrugEntryDialog, type DrugDialogContext } from './DrugEntryDialog';
import { MilestoneEntryDialog } from './MilestoneEntryDialog';
import { VitalsEntryDialog } from './VitalsEntryDialog';

type Dialog = 'drug' | 'vitals' | 'milestone' | null;

/**
 * The Anaesthesia tab (HO-SRG-002 §11.4): the append-only timeline and — only while the log is open and
 * only for the roles the server allows — **Add drug** (vets), **Add vitals** and **Add milestone**
 * (nurses and vets). The log polls every 30 s while open (UI-5); an open dialog is untouched by a poll
 * because its fields are its own state. Nothing on this tab edits or deletes an entry.
 */
export function AnaesthesiaTab({ surgery }: { surgery: SurgeryDetail }) {
  const log = useAnaesthesiaLog(surgery.id, true);
  const roles = useStaffRoles();
  const session = useStaffSession();
  const [dialog, setDialog] = useState<Dialog>(null);
  const [correcting, setCorrecting] = useState<AnaesthesiaEntry | null>(null);

  if (surgery.status === 'Scheduled') {
    return (
      <Banner tone="neutral">The anaesthesia log opens once the cat has been admitted.</Banner>
    );
  }
  if (log.error instanceof ApiForbiddenError) return <AccessRefusedState error={log.error} />;
  if (log.isPending)
    return <LoadingState variant="card" rows={4} label="Loading the anaesthesia log…" />;
  if (!log.data) {
    return (
      <ErrorState
        title="We couldn’t load the anaesthesia log."
        onRetry={() => void log.refetch()}
      />
    );
  }
  const data = log.data;
  const canDrug = roles.has('Vet');
  const canVitals = roles.has('Nurse', 'Vet');
  const caller = session.data
    ? { id: session.data.staffAccountId, name: session.data.fullName }
    : null;
  const administrators = [
    ...(caller && roles.has('Vet') ? [caller] : []),
    { id: surgery.surgeon.id, name: surgery.surgeon.name },
  ].filter((person, i, all) => all.findIndex((p) => p.id === person.id) === i);
  const context: DrugDialogContext = {
    surgeryId: surgery.id,
    catId: surgery.catId,
    zone: data.branchTimeZoneId,
    weight: data.latestWeight,
    administrators,
  };

  return (
    <div className="flex flex-col gap-5">
      {data.isOpen && (canDrug || canVitals) && (
        <div className="flex flex-wrap gap-2" role="group" aria-label="Add to the log">
          {canDrug && (
            <Button onClick={() => setDialog('drug')}>
              <Pill size={16} aria-hidden="true" /> Add drug
            </Button>
          )}
          {canVitals && (
            <>
              <Button variant="secondary" onClick={() => setDialog('vitals')}>
                <Activity size={16} aria-hidden="true" /> Add vitals
              </Button>
              <Button variant="secondary" onClick={() => setDialog('milestone')}>
                <Flag size={16} aria-hidden="true" /> Add milestone
              </Button>
            </>
          )}
        </div>
      )}
      {!data.isOpen && (
        <Banner tone="neutral">The log is closed. It can be read but not added to.</Banner>
      )}
      <AnaesthesiaTimeline
        log={data}
        canCorrect={(entry) =>
          (entry.correctedKind ?? entry.kind) === 'Drug' ? canDrug : canVitals
        }
        onCorrect={setCorrecting}
      />
      {dialog === 'drug' && <DrugEntryDialog context={context} onClose={() => setDialog(null)} />}
      {dialog === 'vitals' && (
        <VitalsEntryDialog
          surgeryId={surgery.id}
          catId={surgery.catId}
          zone={data.branchTimeZoneId}
          onClose={() => setDialog(null)}
        />
      )}
      {dialog === 'milestone' && (
        <MilestoneEntryDialog
          surgeryId={surgery.id}
          catId={surgery.catId}
          zone={data.branchTimeZoneId}
          onClose={() => setDialog(null)}
        />
      )}
      {correcting && (
        <CorrectEntryDialog
          entry={correcting}
          context={context}
          onClose={() => setCorrecting(null)}
        />
      )}
    </div>
  );
}
