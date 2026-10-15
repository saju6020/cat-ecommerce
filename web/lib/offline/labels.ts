/**
 * `lib/offline/labels.ts` — HO-XCT-002 §11.4. Plain-language wording for the codes the snapshot
 * carries. A status is always shown as text; an unknown code is spaced out rather than hidden.
 */

/** `CheckedIn` -> `Checked in`, `InConsultation` -> `In consultation`. */
export function humanizeCode(code: string): string {
  const spaced = code.replace(/([a-z0-9])([A-Z])/g, '$1 $2').trim();
  return spaced.charAt(0).toUpperCase() + spaced.slice(1).toLowerCase();
}

export type OmittedSectionName = 'appointments' | 'boardingTasks';

const SECTION_LABEL: Record<OmittedSectionName, string> = {
  appointments: 'Appointments',
  boardingTasks: 'Boarding tasks',
};

/** The sentence for a section left out of an otherwise valid copy, each reason in its own words. */
export function describeOmittedSection(section: OmittedSectionName, reason: string): string {
  const label = SECTION_LABEL[section];
  const lower = label.toLowerCase();
  if (reason === 'NotPermitted') {
    return `${label} weren't included in this copy. Your role doesn't include ${lower}, so they aren't saved on this device.`;
  }
  if (reason === 'NotAvailable') {
    return `${label} weren't included in this copy. The ${section === 'boardingTasks' ? 'boarding task list' : 'appointment list'} isn't available in PurrCare yet, so there was nothing to save.`;
  }
  return `${label} weren't included in this copy.`;
}
