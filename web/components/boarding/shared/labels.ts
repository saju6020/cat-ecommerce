import type {
  BelongingKind,
  BoardingStayStatus,
  BoardingUnitType,
  CareTaskKind,
  CatMood,
  DepositState,
  FoodSource,
  GateReason,
  IncidentKind,
  IncidentSeverity,
  LitterOutput,
  LitterType,
  PortionEaten,
  StayAttentionReason,
  WaterIntake,
} from '@/lib/api/types';
import { formatDateOnly } from '@/lib/format/time';

/**
 * `components/boarding/shared/labels.ts` — HO-BRD-002 §11: plain-language wording for the
 * enumerations the server sends (HO-BRD-001 §10.1). One place, so the portal, the staff workspace
 * and the task board never describe the same state two ways. The enumeration NAMES are data; they
 * are never shown raw (NFR-USA-4).
 */

export const UNIT_TYPE_LABEL: Record<BoardingUnitType, string> = {
  Standard: 'Standard',
  Deluxe: 'Deluxe',
  Family: 'Family',
  Isolation: 'Isolation',
};

export const STAY_STATUS_LABEL: Record<BoardingStayStatus, string> = {
  Enquiry: 'Not reserved yet',
  Reserved: 'Reserved — not yet confirmed',
  Confirmed: 'Confirmed',
  CheckedIn: 'In the care centre',
  CheckedOut: 'Checked out',
  Cancelled: 'Cancelled',
  NoShow: 'Did not arrive',
};

/** What happens next, in the owner's words (US-BRD-06). */
export const STAY_NEXT_STEP: Record<BoardingStayStatus, string> = {
  Enquiry: 'Finish the booking and reserve a unit.',
  Reserved:
    'Pay the deposit offline. We confirm the stay as soon as the clinic records your payment.',
  Confirmed: 'Bring your cat on the arrival day. We will check them in together.',
  CheckedIn: 'Your cat is with us. The diary shows how each day is going.',
  CheckedOut: 'The stay is over. The summary has everything that happened.',
  Cancelled: 'This stay was cancelled.',
  NoShow: 'The cat did not arrive, so the stay was closed.',
};

export const DEPOSIT_STATE_LABEL: Record<DepositState, string> = {
  NotRequired: 'No deposit needed',
  Awaiting: 'Awaiting payment',
  PendingApproval: 'Payment being checked',
  Received: 'Deposit received',
};

export const DEPOSIT_STATE_HELP: Record<DepositState, string> = {
  NotRequired: 'Nothing to pay up front for this stay.',
  Awaiting: 'We have not recorded your deposit yet. Pay it at the clinic or by bank transfer.',
  PendingApproval:
    'We have a payment for this stay and the clinic is checking it. Your reservation will not expire while it is.',
  Received: 'The clinic has recorded your deposit.',
};

export const GATE_STATUS_LABEL = {
  Passed: 'Vaccinations up to date',
  Blocked: 'Vaccination overdue',
  Overridden: 'Vaccination overridden by an administrator',
} as const;

export const TASK_KIND_LABEL: Record<CareTaskKind, string> = {
  Feed: 'Feeding',
  Medication: 'Medication',
  LitterChange: 'Litter',
  Enrichment: 'Enrichment',
  ObservationCheck: 'Observation',
};

export const PORTION_LABEL: Record<PortionEaten, string> = {
  None: 'None',
  Quarter: '¼',
  Half: '½',
  ThreeQuarters: '¾',
  All: 'All',
};

export const PORTION_ORDER: PortionEaten[] = ['None', 'Quarter', 'Half', 'ThreeQuarters', 'All'];

export const PORTION_SPOKEN: Record<PortionEaten, string> = {
  None: 'None eaten',
  Quarter: 'A quarter eaten',
  Half: 'Half eaten',
  ThreeQuarters: 'Three quarters eaten',
  All: 'All eaten',
};

export const WATER_LABEL: Record<WaterIntake, string> = {
  None: 'No water',
  Low: 'Low',
  Normal: 'Normal',
  High: 'High',
};

export const LITTER_OUTPUT_LABEL: Record<LitterOutput, string> = {
  Normal: 'Normal',
  None: 'None',
  Abnormal: 'Abnormal',
};

export const MOOD_LABEL: Record<CatMood, string> = {
  Relaxed: 'Relaxed',
  Playful: 'Playful',
  Anxious: 'Anxious',
  Withdrawn: 'Withdrawn',
  Aggressive: 'Aggressive',
};

export const INCIDENT_KIND_LABEL: Record<IncidentKind, string> = {
  Injury: 'Injury',
  Illness: 'Illness',
  EscapeAttempt: 'Escape attempt',
  Fight: 'Fight',
  RefusalToEat: 'Refusing food',
  Other: 'Something else',
};

export const SEVERITY_LABEL: Record<IncidentSeverity, string> = {
  Low: 'Low',
  Medium: 'Medium',
  High: 'High',
};

export const BELONGING_KIND_LABEL: Record<BelongingKind, string> = {
  Carrier: 'Carrier',
  Bowl: 'Bowl',
  Toy: 'Toy',
  Bedding: 'Bedding',
  Food: 'Food',
  Medication: 'Medication',
  Other: 'Other',
};

export const LITTER_TYPE_LABEL: Record<LitterType, string> = {
  Clumping: 'Clumping',
  Silica: 'Silica',
  Tofu: 'Tofu',
  Bentonite: 'Bentonite',
  Paper: 'Paper',
  Other: 'Other',
};

export const FOOD_SOURCE_LABEL: Record<FoodSource, string> = {
  OwnFood: 'Own food',
  HouseFood: 'House food',
};

export const ATTENTION_LABEL: Record<StayAttentionReason, string> = {
  CatDeceased: 'A cat on this stay has died.',
  CatOwnerChanged: 'A cat on this stay has a new owner.',
  MaintenanceOverlap: 'This stay overlaps unit maintenance.',
  IsolationUnavailable: 'No isolation unit was free when one was needed.',
  VaccinationGateBlocked:
    'A cat on this reserved stay no longer passes the vaccination gate, so it cannot be confirmed.',
  UnitOverstayed: 'The previous cat in this unit has not left yet: move this booking.',
};

/** A gate reason in plain language, with the vaccine and date (US-BRD-04 AC1). The server's own
 *  sentence wins when it sends one. */
export function describeGateReason(catName: string, reason: GateReason): string {
  if (reason.message) return reason.message;
  const vaccine = reason.vaccineName ? `${reason.vaccineName} vaccination` : 'A core vaccination';
  if (reason.reason === 'NeverAdministered') {
    return `${catName} has no record of a ${vaccine.toLowerCase()} yet.`;
  }
  return reason.dueOn
    ? `${catName}’s ${vaccine.toLowerCase()} is due on ${formatDateOnly(reason.dueOn)}, before the stay ends.`
    : `${catName}’s ${vaccine.toLowerCase()} is overdue.`;
}
