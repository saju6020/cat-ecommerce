'use client';

import { useState } from 'react';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  useDialogReturnFocus,
} from '@/components/ui/dialog';
import { FormErrorAlert, SelectRow, TextAreaRow } from '@/components/boarding/shared/FormBits';
import { INCIDENT_KIND_LABEL, SEVERITY_LABEL } from '@/components/boarding/shared/labels';
import { describeBoardingError } from '@/lib/api/errors';
import type { IncidentKind, IncidentSeverity } from '@/lib/api/types';
import { FormFieldRow } from '@/components/cat/FormFieldRow';
import { zonedLocalToInstant } from '@/lib/format/appointment';
import { timeOnlyInZone, todayInZone } from '@/lib/format/time';
import { useBranchTimeZone } from '@/lib/hooks/useBranchTimeZone';
import { useRecordIncident } from '@/lib/hooks/useBoardingCare';
import { incidentSchema } from '@/lib/validation/boarding';

/**
 * `components/boarding/incidents/IncidentForm.tsx` — HO-BRD-002 §11.9, US-BRD-13: kind, severity
 * (with the explicit note that **medium and high message the owner immediately**), what happened and
 * the immediate action, for one cat of a stay. Prefilled with the cat from the task card's
 * **Incident** button. Values are kept while the server refuses.
 */
export function IncidentForm({
  open,
  onOpenChange,
  stayId,
  cats,
  initialCatId,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  stayId: string;
  cats: Array<{ catId: string; catName: string }>;
  initialCatId?: string;
}) {
  const record = useRecordIncident(stayId);
  const zone = useBranchTimeZone();
  // When it happened, in the branch's own time; defaults to now (a late report can say when it really was).
  const [happenedDate, setHappenedDate] = useState(() => todayInZone(zone));
  const [happenedTime, setHappenedTime] = useState(() =>
    timeOnlyInZone(new Date().toISOString(), zone),
  );
  const [catId, setCatId] = useState(initialCatId ?? cats[0]?.catId ?? '');
  const [kind, setKind] = useState<IncidentKind>('Illness');
  const [severity, setSeverity] = useState<IncidentSeverity>('Low');
  const [description, setDescription] = useState('');
  const [action, setAction] = useState('');
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [serverError, setServerError] = useState<string | null>(null);
  const returnFocusRef = useDialogReturnFocus(open);

  async function submit() {
    const parsed = incidentSchema.safeParse({
      catId,
      kind,
      severity,
      description,
      immediateAction: action,
    });
    if (!parsed.success) {
      const next: Record<string, string> = {};
      for (const issue of parsed.error.issues) next[String(issue.path[0])] ??= issue.message;
      setErrors(next);
      return;
    }
    const occurredOn = zonedLocalToInstant(happenedDate, happenedTime, zone);
    if (!happenedDate || !happenedTime || Number.isNaN(Date.parse(occurredOn))) {
      setErrors({ occurredOn: 'Enter when it happened.' });
      return;
    }
    if (Date.parse(occurredOn) > Date.now() + 60_000) {
      setErrors({ occurredOn: 'That time is in the future.' });
      return;
    }
    setErrors({});
    setServerError(null);
    try {
      await record.mutateAsync({ ...parsed.data, occurredOn });
      setDescription('');
      setAction('');
      setHappenedDate(todayInZone(zone));
      setHappenedTime(timeOnlyInZone(new Date().toISOString(), zone));
      onOpenChange(false);
    } catch (e) {
      setServerError(describeBoardingError(e));
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent hideClose returnFocusRef={returnFocusRef}>
        <DialogHeader>
          <DialogTitle>Record an incident</DialogTitle>
          <DialogDescription>
            Medium and high severity messages the owner immediately. Low severity is kept on the
            record only.
          </DialogDescription>
        </DialogHeader>
        <div className="flex flex-col gap-3">
          <div className="grid gap-3 sm:grid-cols-3">
            <SelectRow
              id="inc-cat"
              label="Cat"
              value={catId}
              error={errors.catId}
              onChange={(e) => setCatId(e.target.value)}
            >
              {cats.map((c) => (
                <option key={c.catId} value={c.catId}>
                  {c.catName}
                </option>
              ))}
            </SelectRow>
            <SelectRow
              id="inc-kind"
              label="What kind"
              value={kind}
              onChange={(e) => setKind(e.target.value as IncidentKind)}
            >
              {Object.entries(INCIDENT_KIND_LABEL).map(([k, v]) => (
                <option key={k} value={k}>
                  {v}
                </option>
              ))}
            </SelectRow>
            <SelectRow
              id="inc-sev"
              label="Severity"
              value={severity}
              onChange={(e) => setSeverity(e.target.value as IncidentSeverity)}
            >
              {Object.entries(SEVERITY_LABEL).map(([k, v]) => (
                <option key={k} value={k}>
                  {v}
                </option>
              ))}
            </SelectRow>
          </div>
          <div className="grid gap-3 sm:grid-cols-2">
            <FormFieldRow
              id="inc-date"
              label="Date it happened"
              type="date"
              value={happenedDate}
              onChange={(e) => setHappenedDate(e.target.value)}
            />
            <FormFieldRow
              id="inc-time"
              label={`Time it happened (${zone})`}
              type="time"
              value={happenedTime}
              error={errors.occurredOn}
              onChange={(e) => setHappenedTime(e.target.value)}
            />
          </div>
          <TextAreaRow
            id="inc-desc"
            label="What happened"
            required
            value={description}
            error={errors.description}
            onChange={(e) => setDescription(e.target.value)}
          />
          <TextAreaRow
            id="inc-action"
            label="What was done straight away"
            required
            value={action}
            error={errors.immediateAction}
            onChange={(e) => setAction(e.target.value)}
          />
          <FormErrorAlert message={serverError} />
        </div>
        <DialogFooter>
          <Button variant="secondary" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button onClick={() => void submit()} disabled={record.isPending}>
            {record.isPending ? 'Saving…' : 'Record incident'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
