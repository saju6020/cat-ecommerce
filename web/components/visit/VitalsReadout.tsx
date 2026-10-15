import { TriangleAlert } from 'lucide-react';
import { formatMeasurement, vitalSignLabel } from '@/lib/format/clinical';
import { formatInZone } from '@/lib/format/time';
import type { VitalSign, VitalsEntry } from '@/lib/api/visits';

/**
 * `VitalsReadout` — HO-EMR-002 §11.2. One vitals entry as text: every sign with its UNIT through
 * `formatMeasurement` (UI-8), and a sign the recorder confirmed outside the usual range carries the
 * words *"confirmed outside usual range"* with an icon (never colour alone). Entries are shown newest
 * first by the caller; this renders one.
 */
const SIGNS: Array<{ sign: VitalSign; key: keyof VitalsEntry }> = [
  { sign: 'Weight', key: 'weight' },
  { sign: 'Temperature', key: 'temperature' },
  { sign: 'HeartRate', key: 'heartRate' },
  { sign: 'RespiratoryRate', key: 'respiratoryRate' },
  { sign: 'BodyConditionScore', key: 'bodyConditionScore' },
  { sign: 'PainScore', key: 'painScore' },
];

export function VitalsReadout({ entry, zone }: { entry: VitalsEntry; zone: string }) {
  const present = SIGNS.filter(({ key }) => entry[key] !== null);
  return (
    <div className="flex flex-col gap-2">
      <p className="text-xs text-ink-muted">
        {formatInZone(entry.measuredOn, zone)} · {entry.recordedBy}
      </p>
      <dl className="m-0 grid grid-cols-2 gap-x-4 gap-y-2 text-sm sm:grid-cols-3">
        {present.map(({ sign, key }) => (
          <div key={sign} className="min-w-0">
            <dt className="text-[11px] font-semibold uppercase tracking-wide text-ink-muted">
              {vitalSignLabel(sign)}
            </dt>
            <dd className="m-0 font-semibold text-ink">
              {formatMeasurement(entry[key] as VitalsEntry['weight'])}
              {entry.confirmedOutOfRange.includes(sign) && (
                <span className="mt-0.5 flex items-center gap-1 text-xs font-semibold text-warning-ink">
                  <TriangleAlert size={12} aria-hidden="true" />
                  confirmed outside usual range
                </span>
              )}
            </dd>
          </div>
        ))}
      </dl>
      {entry.note && <p className="whitespace-pre-wrap text-sm text-ink-muted">{entry.note}</p>}
    </div>
  );
}
