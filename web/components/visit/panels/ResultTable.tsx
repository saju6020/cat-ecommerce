import { FlagBadge } from '../FlagBadge';
import { formatRange, formatValueWithUnit } from '@/lib/format/clinical';
import { formatDateOnly, formatInZone } from '@/lib/format/time';
import type { DiagnosticResultSet, DiagnosticResultValue } from '@/lib/api/diagnostics';

/**
 * `ResultTable` — HO-EMR-002 §11.3 (US-CLN-14). Result sets with each value, its UNIT, the range it was
 * judged against and the server's flag as text plus icon (`FlagBadge`, UI-6). A set that a later set
 * supersedes is collapsed under *"Superseded on …"* — it stays on the record (results are append-only)
 * but is no longer the current one. Plain text throughout (ADR-FE-058).
 */
function valueText(value: DiagnosticResultValue): string {
  return value.valueKind === 'Numeric'
    ? formatValueWithUnit(value.numericValue ?? '', value.unit)
    : (value.textValue ?? '');
}

function rangeText(value: DiagnosticResultValue): string {
  if (value.valueKind === 'Qualitative') {
    return value.normalText ? `Normal: ${value.normalText}` : 'No normal value stated';
  }
  return formatRange(value.referenceLow, value.referenceHigh, value.unit) ?? 'No range stated';
}

function SetTable({
  set,
  zone,
  showRecorder,
}: {
  set: DiagnosticResultSet;
  zone: string;
  showRecorder: boolean;
}) {
  return (
    <div className="flex flex-col gap-2">
      <p className="text-xs text-ink-muted">
        Reported {formatDateOnly(set.reportedOn)}
        {showRecorder && ` · recorded by ${set.recordedBy} · ${formatInZone(set.recordedOn, zone)}`}
      </p>
      <div className="overflow-x-auto">
        <table className="w-full min-w-[28rem] border-collapse text-left text-sm">
          <caption className="sr-only">
            Result values reported {formatDateOnly(set.reportedOn)}
          </caption>
          <thead>
            <tr className="border-b border-border text-xs uppercase tracking-wide text-ink-muted">
              <th scope="col" className="py-1.5 pr-3 font-semibold">
                Test
              </th>
              <th scope="col" className="py-1.5 pr-3 font-semibold">
                Result
              </th>
              <th scope="col" className="py-1.5 pr-3 font-semibold">
                Range
              </th>
              <th scope="col" className="py-1.5 font-semibold">
                Flag
              </th>
            </tr>
          </thead>
          <tbody>
            {set.values.map((value) => (
              <tr key={value.id} className="border-b border-border last:border-0">
                <th scope="row" className="py-2 pr-3 font-semibold text-ink">
                  {value.name}
                </th>
                <td className="py-2 pr-3 text-ink">{valueText(value)}</td>
                <td className="py-2 pr-3 text-ink-muted">{rangeText(value)}</td>
                <td className="py-2">
                  <FlagBadge flag={value.flag} />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {set.note && <p className="whitespace-pre-wrap text-sm text-ink-muted">{set.note}</p>}
    </div>
  );
}

export function ResultTable({
  sets,
  zone,
  showRecorder = true,
}: {
  sets: DiagnosticResultSet[];
  zone: string;
  /** The owner's portal hides who recorded a set and when (staff detail). */
  showRecorder?: boolean;
}) {
  if (sets.length === 0) return <p className="text-sm text-ink-muted">No results yet.</p>;
  const supersededBy = new Map<string, DiagnosticResultSet>();
  sets.forEach((set) => {
    if (set.supersedesResultSetId) supersededBy.set(set.supersedesResultSetId, set);
  });
  const ordered = [...sets].sort((a, b) => b.recordedOn.localeCompare(a.recordedOn));
  return (
    <div className="flex flex-col gap-4">
      {ordered.map((set) => {
        const replacement = supersededBy.get(set.id);
        if (!replacement)
          return <SetTable key={set.id} set={set} zone={zone} showRecorder={showRecorder} />;
        return (
          <details key={set.id} className="rounded-[var(--radius-sm)] border border-border p-3">
            <summary className="cursor-pointer text-sm font-semibold text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent">
              Superseded on {formatDateOnly(replacement.reportedOn)}
            </summary>
            <div className="mt-3">
              <SetTable set={set} zone={zone} showRecorder={showRecorder} />
            </div>
          </details>
        );
      })}
    </div>
  );
}
