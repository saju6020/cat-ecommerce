import { BRANCH_INFO } from '../appointments/store';
import { findCode, type StoredVisit } from './store';

/**
 * `mocks/handlers/visits/summary.ts` — `DischargeSummaryViewModel` (HO-EMR-001 §8.8) derived from a
 * finalised visit, exactly as the real read side does: `treatments` and `medicinesAdministered` are
 * `[]` (Phase F is not built) and `prescriptionsAvailable` is `false` (HO-PHM-001 has not registered
 * a contributor), so the printed summary says medicines to take home are on the prescription label.
 */
export function dischargeSummaryFor(visit: StoredVisit) {
  const branch = BRANCH_INFO[visit.branchId];
  const weight = [...visit.vitals].reverse().find((v) => v.weightKg !== null)?.weightKg ?? null;
  return {
    visitId: visit.id,
    branchName: branch?.name ?? 'Clinic',
    branchAddress: '12 Gulshan Avenue, Dhaka',
    branchPhone: branch?.phone ?? null,
    branchTimeZoneId: branch?.zone ?? 'UTC',
    catId: visit.catId,
    catName: visit.catName,
    catSex: 'Female',
    catAge: '3 years',
    weightAtVisit: weight === null ? null : { value: weight.toFixed(2), unit: 'kg' },
    visitDate: visit.openedOn.slice(0, 10),
    attendingVet: visit.finalisedBy ?? visit.openedBy,
    problems: visit.problems.map((p) => {
      const code = findCode(p.diagnosisCodeId);
      return { name: code?.name ?? '', code: code?.code ?? '', certainty: p.certainty };
    }),
    treatments: [] as string[],
    medicinesAdministered: [] as string[],
    homeCareInstructions: visit.note.dischargeInstructions,
    followUp: visit.followUp
      ? { kind: visit.followUp.kind, dueOn: visit.followUp.dueOn, appointmentStartsOn: null }
      : null,
    corrections: visit.addenda.map((a) => ({ addedOn: a.addedOn, text: a.text, reason: a.reason })),
    extraSections: [] as Array<{ title: string; lines: string[] }>,
    prescriptionsAvailable: false,
  };
}
