'use client';

import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { toast } from '@/components/ui/use-toast';
import { describeSurgeryError } from '@/lib/api/errors';
import { ASA_GRADES } from '@/lib/format/surgery';
import { useRecordAsaGrade } from '@/lib/hooks/useSurgeries';
import type { AsaGrade, SurgeryDetail } from '@/lib/api/surgeries';
import { errorClass } from './styles';

/**
 * `AsaGradeControl` — HO-SRG-002 §11.3. Vets only (the server's `Surgery.AssessRisk`): grades I to V as a
 * radio group, plus *Emergency (E)*. It records the choice and nothing more; whether the item is then
 * "done" is the server's answer on the refetched checklist.
 */
export function AsaGradeControl({ surgery }: { surgery: SurgeryDetail }) {
  const record = useRecordAsaGrade(surgery.id);
  const [grade, setGrade] = useState<AsaGrade | ''>(surgery.asaGrade ?? '');
  const [emergency, setEmergency] = useState(surgery.asaIsEmergency);
  const [error, setError] = useState<string | null>(null);

  async function save() {
    if (!grade) {
      setError('Choose a grade from I to V.');
      return;
    }
    setError(null);
    try {
      await record.mutateAsync({ grade, isEmergency: emergency });
      toast({ title: 'ASA grade recorded' });
    } catch (failure) {
      setError(describeSurgeryError(failure));
    }
  }

  return (
    <fieldset className="flex flex-col gap-2">
      <legend className="text-sm font-semibold text-ink">ASA grade</legend>
      <div className="flex flex-wrap gap-3">
        {ASA_GRADES.map((value) => (
          <label key={value} className="flex items-center gap-1.5 text-sm text-ink">
            <input
              type="radio"
              name={`asa-${surgery.id}`}
              value={value}
              checked={grade === value}
              onChange={() => setGrade(value)}
              className="h-4 w-4"
            />
            {value}
          </label>
        ))}
        <label className="flex items-center gap-1.5 text-sm text-ink">
          <input
            type="checkbox"
            checked={emergency}
            onChange={(event) => setEmergency(event.target.checked)}
            className="h-4 w-4"
          />
          Emergency (E)
        </label>
      </div>
      {error && (
        <p role="alert" className={errorClass}>
          {error}
        </p>
      )}
      <div>
        <Button
          variant="secondary"
          size="sm"
          onClick={() => void save()}
          disabled={record.isPending}
        >
          {record.isPending ? 'Saving…' : 'Save ASA grade'}
        </Button>
      </div>
    </fieldset>
  );
}
