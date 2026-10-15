'use client';

import { useState } from 'react';
import { Lock } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Banner, DefinitionList, Panel } from '@/components/inventory/fields';
import { toast } from '@/components/ui/use-toast';
import { describeSurgeryError } from '@/lib/api/errors';
import { formatAppointmentWhen } from '@/lib/format/appointment';
import { useAddAddendum } from '@/lib/hooks/useSurgicalReport';
import { SURGERY_LIMITS } from '@/lib/validation/surgery';
import type { SurgicalReport } from '@/lib/api/surgical-reports';
import { TextAreaBox } from '@/components/surgery/TextAreaBox';

/** Free text from the clinic, shown as plain text with its line breaks and never as markup. */
function Text({ children }: { children: string | null }) {
  return <span className="whitespace-pre-wrap break-words">{children || '—'}</span>;
}

/**
 * ⭐ `SurgicalReportView` — HO-SRG-002 §11.5, ADR-FE-074, FR-SRG-5. A finalised report: read-only text
 * ("Finalised by Dr Rahman on …"), the addenda in order with author and time, and — for vets — **Add
 * addendum**. There is no editor, no edit control and no delete control for the report or an addendum
 * (HO-SRG-002 §20.6); the only way to add to it is an addendum.
 */
export function SurgicalReportView({
  report,
  surgeryId,
  canAddAddendum,
}: {
  report: SurgicalReport;
  surgeryId: string;
  canAddAddendum: boolean;
}) {
  const zone = report.branchTimeZoneId;
  const add = useAddAddendum(surgeryId);
  const [text, setText] = useState('');
  const [error, setError] = useState<string | null>(null);
  const finalised = report.status === 'Finalised';

  async function submit() {
    if (!text.trim()) {
      setError('Write the addendum.');
      return;
    }
    setError(null);
    try {
      await add.mutateAsync(text.trim());
      setText('');
      toast({ title: 'Addendum added' });
    } catch (failure) {
      setError(describeSurgeryError(failure));
    }
  }

  return (
    <div className="flex max-w-3xl flex-col gap-4">
      {finalised ? (
        <Banner tone="info">
          <span className="flex items-center gap-2">
            <Lock size={14} aria-hidden="true" />
            Finalised by {report.finalisedByName ?? 'a vet'}
            {report.finalisedOn ? ` on ${formatAppointmentWhen(report.finalisedOn, zone)}` : ''}. It
            can only be added to by addendum.
          </span>
        </Banner>
      ) : (
        <Banner tone="neutral">This report is still a draft.</Banner>
      )}
      <DefinitionList
        className="rounded-[var(--radius-md)] border border-border bg-surface p-4"
        items={[
          { label: 'Procedure performed', value: <Text>{report.procedurePerformed}</Text> },
          { label: 'Findings', value: <Text>{report.findings}</Text> },
          { label: 'Technique', value: <Text>{report.technique}</Text> },
          { label: 'Suture material', value: <Text>{report.sutureMaterial}</Text> },
          {
            label: 'Complications',
            value: report.hadComplications ? <Text>{report.complications}</Text> : 'None',
          },
          { label: 'Surgeon', value: report.surgeonName },
          { label: 'Assistant', value: report.assistantName },
        ]}
      />
      <Panel title="Addenda" headingId="srg-addenda">
        {report.addenda.length === 0 ? (
          <p className="text-sm text-ink-muted">No addenda.</p>
        ) : (
          <ol className="flex flex-col gap-3">
            {report.addenda.map((addendum) => (
              <li key={addendum.id} className="text-sm text-ink">
                <p className="text-xs text-ink-muted">
                  {addendum.addedByName} · {formatAppointmentWhen(addendum.addedOn, zone)}
                </p>
                <Text>{addendum.text}</Text>
              </li>
            ))}
          </ol>
        )}
        {finalised && canAddAddendum && (
          <div className="mt-4 flex flex-col gap-2">
            <TextAreaBox
              id="addendum-text"
              label="Add addendum"
              value={text}
              onChange={setText}
              max={SURGERY_LIMITS.addendumMax}
              error={error ?? undefined}
              hint="An addendum is added to the report and cannot be edited or removed."
            />
            <div>
              <Button variant="secondary" onClick={() => void submit()} disabled={add.isPending}>
                {add.isPending ? 'Adding…' : 'Add addendum'}
              </Button>
            </div>
          </div>
        )}
      </Panel>
    </div>
  );
}
