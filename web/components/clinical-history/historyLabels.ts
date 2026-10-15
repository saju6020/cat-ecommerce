import type { HistorySectionKey } from '@/lib/api/clinical-history';

/** Tab / list labels per section key (HO-EMR-002 §11.4). The server decides which exist and in which order. */
export const SECTION_LABELS: Readonly<Record<HistorySectionKey, string>> = {
  timeline: 'Timeline',
  visits: 'Visits',
  diagnoses: 'Diagnoses',
  'lab-results': 'Lab results',
  photos: 'Photos',
  vaccinations: 'Vaccinations',
  'parasite-treatments': 'Parasite control',
  weights: 'Weights',
  prescriptions: 'Prescriptions',
  'boarding-stays': 'Boarding stays',
  'food-purchases': 'Food purchases',
};

export function sectionLabel(key: string): string {
  return SECTION_LABELS[key as HistorySectionKey] ?? key;
}

/**
 * `SectionUnavailable`'s sentence, chosen from the SECTION KEY — the server's `owner` (a handover id)
 * is never shown. The areas behind these sections are not built yet (HO-EMR-001 §4.2).
 */
const UNAVAILABLE_COPY: Readonly<Record<string, string>> = {
  prescriptions: 'Prescriptions will appear here once the pharmacy module is live.',
  'boarding-stays': 'Boarding stays will appear here once the boarding module is live.',
  'food-purchases': 'Food purchases will appear here once the shop orders module is live.',
};

export function unavailableCopy(key: string): string {
  return UNAVAILABLE_COPY[key] ?? `${sectionLabel(key)} will appear here once that module is live.`;
}
