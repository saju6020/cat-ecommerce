'use client';

import { useState } from 'react';
import { Pause, Play, Send, XCircle } from 'lucide-react';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import { Button } from '@/components/ui/button';
import { useToast } from '@/components/ui/use-toast';
import { describeReportError } from '@/lib/api/errors';
import { describeRunFailure } from '@/lib/format/report';
import { useRunScheduleNow, useScheduleTransition } from '@/lib/hooks/useReportSchedules';

type Dialog = 'pause' | 'cancel' | 'run' | null;

function people(count: number): string {
  return `${count} ${count === 1 ? 'person' : 'people'}`;
}

/**
 * `components/report-schedules/ScheduleActions.tsx` — HO-RPT-002 §11.5, UI-3, U-E6. **Pause**,
 * **Resume**, **Cancel** and **Run now**. Pause, Cancel and Run now each confirm in words that name
 * the schedule and how many people are affected; Cancel also says past runs are kept. Run now ends
 * in a result toast ("Generated — 412 rows") and the run list refreshes. A cancelled schedule has
 * no actions at all.
 */
export function ScheduleActions({
  scheduleId,
  name,
  status,
  recipientCount,
}: {
  scheduleId: string;
  name: string;
  status: string;
  recipientCount: number;
}) {
  const [dialog, setDialog] = useState<Dialog>(null);
  const [failure, setFailure] = useState<string | null>(null);
  const transition = useScheduleTransition(scheduleId);
  const runNow = useRunScheduleNow(scheduleId);
  const { toast } = useToast();

  if (status === 'Cancelled') return null;

  async function change(action: 'pause' | 'resume' | 'cancel') {
    setFailure(null);
    try {
      await transition.mutateAsync(action);
      setDialog(null);
    } catch (error) {
      setDialog(null);
      setFailure(describeReportError(error));
    }
  }

  async function run() {
    setFailure(null);
    try {
      const result = await runNow.mutateAsync();
      setDialog(null);
      if (result.status === 'Failed') {
        toast({
          title: 'The run failed',
          description: describeRunFailure(result.failureCode),
          variant: 'destructive',
        });
      } else {
        toast({ title: `Generated — ${result.rowCount} rows` });
      }
    } catch (error) {
      setDialog(null);
      setFailure(describeReportError(error));
    }
  }

  return (
    <div className="flex flex-col gap-2">
      <div className="flex flex-wrap gap-2">
        {status === 'Active' && (
          <Button variant="secondary" onClick={() => setDialog('pause')}>
            <Pause size={14} aria-hidden="true" /> Pause
          </Button>
        )}
        {status === 'Paused' && (
          <Button
            variant="secondary"
            onClick={() => void change('resume')}
            disabled={transition.isPending}
          >
            <Play size={14} aria-hidden="true" /> Resume
          </Button>
        )}
        <Button variant="secondary" onClick={() => setDialog('run')}>
          <Send size={14} aria-hidden="true" /> Run now
        </Button>
        <Button variant="destructive" onClick={() => setDialog('cancel')}>
          <XCircle size={14} aria-hidden="true" /> Cancel schedule
        </Button>
      </div>
      {failure && (
        <p role="alert" className="m-0 text-sm font-semibold text-danger-ink">
          {failure}
        </p>
      )}

      <ConfirmDialog
        open={dialog === 'pause'}
        onOpenChange={(open) => !open && setDialog(null)}
        title="Pause this schedule?"
        consequence={`‘${name}’ will stop being emailed to ${people(recipientCount)} until you resume it. Past runs are kept.`}
        confirmLabel="Pause"
        onConfirm={() => change('pause')}
      />
      <ConfirmDialog
        open={dialog === 'cancel'}
        onOpenChange={(open) => !open && setDialog(null)}
        title="Cancel this schedule?"
        consequence={`‘${name}’ will stop being emailed to ${people(recipientCount)}. Past runs are kept. A cancelled schedule cannot be resumed.`}
        confirmLabel="Cancel schedule"
        variant="destructive"
        onConfirm={() => change('cancel')}
      />
      <ConfirmDialog
        open={dialog === 'run'}
        onOpenChange={(open) => !open && setDialog(null)}
        title="Run this schedule now?"
        consequence={`This generates the report and emails it to ${people(recipientCount)} now.`}
        confirmLabel="Run now"
        pendingLabel="Running…"
        onConfirm={run}
      />
    </div>
  );
}
