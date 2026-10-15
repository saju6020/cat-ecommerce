import {
  AlertTriangle,
  Ban,
  CheckCircle2,
  Clock,
  Hourglass,
  LogIn,
  LogOut,
  Siren,
  Timer,
} from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import type {
  BoardingStayStatus,
  DepositState,
  IncidentSeverity,
  VaccinationGateStatus,
} from '@/lib/api/types';
import { formatOverdue } from '@/lib/format/boarding';
import {
  DEPOSIT_STATE_LABEL,
  GATE_STATUS_LABEL,
  SEVERITY_LABEL,
  STAY_STATUS_LABEL,
} from './labels';

/**
 * `components/boarding/shared/StatusBadges.tsx` — HO-BRD-002 UI-6: severity, gate status, deposit
 * state, stay status and overdue are **text plus** colour and an icon, never colour alone.
 */

const ICON = 12;

export function StayStatusBadge({ status }: { status: BoardingStayStatus }) {
  const label = STAY_STATUS_LABEL[status];
  switch (status) {
    case 'Confirmed':
    case 'CheckedIn':
      return (
        <Badge variant="success" icon={<CheckCircle2 size={ICON} aria-hidden="true" />}>
          {label}
        </Badge>
      );
    case 'Reserved':
    case 'Enquiry':
      return (
        <Badge variant="warning" icon={<Hourglass size={ICON} aria-hidden="true" />}>
          {label}
        </Badge>
      );
    case 'Cancelled':
    case 'NoShow':
      return (
        <Badge variant="danger" icon={<Ban size={ICON} aria-hidden="true" />}>
          {label}
        </Badge>
      );
    default:
      return (
        <Badge variant="neutral" icon={<LogOut size={ICON} aria-hidden="true" />}>
          {label}
        </Badge>
      );
  }
}

/** `stayStatus` 'Enquiry': the API sends 0.00 / NotRequired before the stay is reserved, which is not "no deposit" -
 *  the deposit is worked out at reserve, so say that (TEST row 77, ACCEPTANCE row 86). */
export function DepositStateBadge({
  state,
  stayStatus,
}: {
  state: DepositState;
  stayStatus?: string;
}) {
  if (stayStatus === 'Enquiry') return <Badge variant="neutral">Deposit set at reserve</Badge>;
  const label = DEPOSIT_STATE_LABEL[state];
  if (state === 'Received') {
    return (
      <Badge variant="success" icon={<CheckCircle2 size={ICON} aria-hidden="true" />}>
        {label}
      </Badge>
    );
  }
  if (state === 'PendingApproval') {
    return (
      <Badge variant="neutral" icon={<Clock size={ICON} aria-hidden="true" />}>
        {label}
      </Badge>
    );
  }
  if (state === 'Awaiting') {
    return (
      <Badge variant="warning" icon={<Hourglass size={ICON} aria-hidden="true" />}>
        {label}
      </Badge>
    );
  }
  return <Badge variant="neutral">{label}</Badge>;
}

export function SeverityBadge({ severity }: { severity: IncidentSeverity }) {
  const label = `${SEVERITY_LABEL[severity]} severity`;
  if (severity === 'High') {
    return (
      <Badge variant="danger" icon={<Siren size={ICON} aria-hidden="true" />}>
        {label}
      </Badge>
    );
  }
  if (severity === 'Medium') {
    return (
      <Badge variant="warning" icon={<AlertTriangle size={ICON} aria-hidden="true" />}>
        {label}
      </Badge>
    );
  }
  return <Badge variant="neutral">{label}</Badge>;
}

export function GateStatusBadge({ status }: { status: VaccinationGateStatus }) {
  const label = GATE_STATUS_LABEL[status];
  if (status === 'Blocked') {
    return (
      <Badge variant="danger" icon={<AlertTriangle size={ICON} aria-hidden="true" />}>
        {label}
      </Badge>
    );
  }
  if (status === 'Overridden') {
    return (
      <Badge variant="warning" icon={<AlertTriangle size={ICON} aria-hidden="true" />}>
        {label}
      </Badge>
    );
  }
  return (
    <Badge variant="success" icon={<CheckCircle2 size={ICON} aria-hidden="true" />}>
      {label}
    </Badge>
  );
}

/** The overdue chip: the minutes come from the SERVER (ADR-FE-105). */
export function OverdueChip({ minutesLate }: { minutesLate: number | null }) {
  return (
    <Badge variant="danger" icon={<Timer size={ICON} aria-hidden="true" />}>
      {formatOverdue(minutesLate)}
    </Badge>
  );
}

export function ArrivalBadge({ label }: { label: string }) {
  return (
    <Badge variant="neutral" icon={<LogIn size={ICON} aria-hidden="true" />}>
      {label}
    </Badge>
  );
}
