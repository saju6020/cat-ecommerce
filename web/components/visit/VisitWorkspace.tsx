'use client';

import { useState } from 'react';
import Link from 'next/link';
import { Button, buttonVariants } from '@/components/ui/button';
import { toast } from '@/components/ui/use-toast';
import { HistoryDrawer } from '@/components/clinical-history/HistoryDrawer';
import { AddendumForm, AddendumList } from './AddendumSection';
import { ConsultationEditor } from './ConsultationEditor';
import { VisitCatHeader } from './VisitCatHeader';
import { VisitHeaderBar } from './VisitHeaderBar';
import { VisitRecordView } from './VisitRecordView';
import { DiagnosticsPanel } from './panels/DiagnosticsPanel';
import { FollowUpPanel } from './panels/FollowUpPanel';
import { PhotosPanel } from './panels/PhotosPanel';
import { VitalsPanel } from './vitals/VitalsPanel';
import { describeAppointmentError } from '@/lib/api/errors';
import { useCompleteAppointment } from '@/lib/hooks/useStaffAppointments';
import { useStaffSession } from '@/lib/hooks/useSession';
import { useAddAddendum } from '@/lib/hooks/useVisit';
import type { VisitDetail } from '@/lib/api/visits';

/**
 * ⭐ `VisitWorkspace` — HO-EMR-002 §11.1 (ADR-FE-049, ADR-FE-051). The ONE route for a consultation:
 * decides, from the visit's `status` and the server's `allowedActions` only, between
 *  - the EDITABLE workspace (`editNote`): note, problems, auto-save, Save, Finalise;
 *  - a READ-ONLY open visit (nurse, administrator): the record as text, with whichever panels they
 *    may act on (vitals for the nurse), and no Save or Finalise;
 *  - a FINALISED record (`VisitRecordView`) with addenda, and *Complete appointment* when the
 *    appointment is still in consultation.
 * History, diagnostic orders and summaries open in a drawer or dialog OVER this page; the form stays
 * mounted, so the draft cannot be lost to looking something up.
 */
export function VisitWorkspace({ visit }: { visit: VisitDetail }) {
  const [historyOpen, setHistoryOpen] = useState(false);
  const roles = useStaffSession().data?.roleIds ?? [];
  const canWriteResults =
    visit.allowedActions.includes('editNote') ||
    (visit.status === 'Finalised' && roles.includes('Vet'));
  const catHeader = <VisitCatHeader catId={visit.catId} />;
  const open = () => setHistoryOpen(true);

  const panels = (
    <>
      <VitalsPanel visit={visit} />
      <DiagnosticsPanel visit={visit} canWriteResults={canWriteResults} />
      <PhotosPanel visit={visit} />
      <FollowUpPanel visit={visit} />
    </>
  );

  let body;
  if (visit.status === 'Open' && visit.allowedActions.includes('editNote')) {
    body = (
      <ConsultationEditor
        visit={visit}
        catHeader={catHeader}
        panels={panels}
        onOpenHistory={open}
      />
    );
  } else if (visit.status === 'Open') {
    body = (
      <div className="flex flex-col gap-5">
        <VisitHeaderBar visit={visit} onOpenHistory={open} />
        {catHeader}
        <div className="grid items-start gap-5 lg:grid-cols-[minmax(0,1.5fr)_minmax(0,1fr)]">
          <div className="min-w-0 rounded-[var(--radius-md)] border border-border bg-surface p-4">
            <VisitRecordView visit={visit} />
          </div>
          <div className="flex min-w-0 flex-col gap-5">{panels}</div>
        </div>
      </div>
    );
  } else {
    body = (
      <FinalisedRecord
        visit={visit}
        catHeader={catHeader}
        canComplete={roles.includes('Vet') || roles.includes('Administrator')}
        onOpenHistory={open}
        canWriteResults={canWriteResults}
      />
    );
  }

  return (
    <>
      {body}
      <HistoryDrawer
        open={historyOpen}
        onOpenChange={setHistoryOpen}
        catId={visit.catId}
        catName={visit.catName}
      />
    </>
  );
}

function FinalisedRecord({
  visit,
  catHeader,
  canComplete,
  onOpenHistory,
  canWriteResults,
}: {
  visit: VisitDetail;
  catHeader: React.ReactNode;
  canComplete: boolean;
  onOpenHistory: () => void;
  canWriteResults: boolean;
}) {
  const add = useAddAddendum(visit.id, visit.catId);
  const complete = useCompleteAppointment();
  const inConsultation = visit.appointment.status === 'InConsultation';
  return (
    <div className="flex flex-col gap-5">
      <VisitHeaderBar visit={visit} onOpenHistory={onOpenHistory} />
      {catHeader}
      <div className="flex flex-wrap gap-2">
        {inConsultation && canComplete && (
          <Button
            onClick={async () => {
              try {
                await complete.mutateAsync(visit.appointment.id);
                toast({ title: 'Appointment completed' });
              } catch (error) {
                toast({
                  title: 'The appointment was not completed',
                  description: describeAppointmentError(error),
                });
              }
            }}
            disabled={complete.isPending}
          >
            Complete appointment
          </Button>
        )}
        <Link
          href={`/staff/visits/${visit.id}/summary`}
          className={buttonVariants({ variant: 'secondary' })}
        >
          Discharge summary
        </Link>
      </div>
      <div className="grid items-start gap-5 lg:grid-cols-[minmax(0,1.5fr)_minmax(0,1fr)]">
        <div className="flex min-w-0 flex-col gap-6 rounded-[var(--radius-md)] border border-border bg-surface p-4">
          <VisitRecordView visit={visit}>
            <AddendumList addenda={visit.addenda} zone={visit.branchTimeZoneId} />
          </VisitRecordView>
          {visit.allowedActions.includes('addAddendum') && (
            <AddendumForm
              onSubmit={async (text, reason) => {
                await add.mutateAsync({ text, ...(reason ? { reason } : {}) });
                toast({ title: 'Addendum added' });
              }}
            />
          )}
        </div>
        <div className="flex min-w-0 flex-col gap-5">
          <DiagnosticsPanel visit={visit} canWriteResults={canWriteResults} />
        </div>
      </div>
    </div>
  );
}
