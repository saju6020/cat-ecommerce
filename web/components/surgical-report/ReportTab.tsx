'use client';

import { useEffect, useRef, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { ErrorState } from '@/components/common/ErrorState';
import { LoadingState } from '@/components/common/LoadingState';
import { Banner } from '@/components/inventory/fields';
import { useStaffRoles } from '@/lib/hooks/useStaffRoles';
import { pendingReportSave } from '@/lib/hooks/useReportAutosave';
import { useSurgicalReport } from '@/lib/hooks/useSurgicalReport';
import type { SurgeryDetail } from '@/lib/api/surgeries';
import { EndProcedurePanel } from './EndProcedurePanel';
import { NeuterStatusNotice } from './NeuterStatusNotice';
import { SurgicalReportEditor } from './SurgicalReportEditor';
import { SurgicalReportView } from './SurgicalReportView';

/**
 * The Report tab (HO-SRG-002 §11.5): end the procedure while in theatre; after it, the draft editor
 * (vets) or the finalised, read-only report with its addenda. The neuter-status notice sits on top for a
 * completed sterilisation. Before the procedure has ended there is nothing to write yet.
 */
export function ReportTab({ surgery }: { surgery: SurgeryDetail }) {
  const roles = useStaffRoles();
  const queryClient = useQueryClient();
  const ended = surgery.status === 'Recovery' || surgery.status === 'Discharged';
  const isVet = roles.has('Vet');
  const report = useSurgicalReport(surgery.id);
  // The editor is keyed so a RELOAD (after a conflict) remounts it with the latest server text. A save also
  // updates the cached report (new version), which must NOT remount the editor under the typist, so the key
  // is the version seen when the editor first appeared, plus a counter that only a reload bumps.
  const editorBase = useRef<string | null>(null);
  const [reloads, setReloads] = useState(0);
  const shown = useRef(false);
  // Coming back before the unmount flush has answered: wait for it, then read the report again, so the
  // editor never opens on the text from before the flush.
  const [waitingForSave, setWaitingForSave] = useState(
    () => pendingReportSave(surgery.id) !== null,
  );
  const refetchReport = report.refetch;
  useEffect(() => {
    const pending = pendingReportSave(surgery.id);
    if (!pending) {
      setWaitingForSave(false);
      return;
    }
    let live = true;
    void pending.then(async () => {
      await refetchReport();
      if (live) setWaitingForSave(false);
    });
    return () => {
      live = false;
    };
  }, [surgery.id, refetchReport]);

  if (surgery.status === 'InTheatre') {
    return <EndProcedurePanel surgery={surgery} canEnd={isVet} />;
  }
  if (!ended) {
    return <Banner tone="neutral">The report starts once the procedure has ended.</Banner>;
  }
  // On a return to the tab the cached report may be stale (marked so by a first save): wait for the read
  // before showing the editor, once. After that a background refetch never replaces the editor.
  if (waitingForSave || report.isPending || (report.isFetching && !shown.current)) {
    return <LoadingState variant="card" rows={3} label="Loading the report…" />;
  }
  if (report.isError) {
    return (
      <ErrorState title="We couldn’t load the report." onRetry={() => void report.refetch()} />
    );
  }

  const data = report.data ?? null;
  shown.current = true;
  editorBase.current ??= String(data?.version ?? 'new');
  const reload = () => {
    void queryClient.invalidateQueries({ queryKey: ['surgeries', surgery.id] });
    void report.refetch().then(() => {
      editorBase.current = null;
      setReloads((count) => count + 1);
    });
  };
  return (
    <div className="flex flex-col gap-5">
      <NeuterStatusNotice surgery={surgery} canApply={isVet} />
      {data?.status === 'Finalised' || (data && !isVet) ? (
        <SurgicalReportView report={data} surgeryId={surgery.id} canAddAddendum={isVet} />
      ) : isVet ? (
        <SurgicalReportEditor
          key={`${editorBase.current}-${reloads}`}
          surgeryId={surgery.id}
          zone={surgery.branchTimeZoneId}
          report={data}
          procedureName={surgery.procedure.name}
          surgeonId={surgery.surgeon.id}
          assistantId={surgery.assistant.id}
          surgeonName={surgery.surgeon.name}
          assistantName={surgery.assistant.name}
          canFinalise
          onReload={reload}
        />
      ) : (
        <Banner tone="neutral">No report has been written yet. A vet writes it.</Banner>
      )}
    </div>
  );
}
