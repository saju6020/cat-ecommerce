import { z } from 'zod';
import type {
  ReportFrequency,
  ReportScheduleDetail,
  ReportScheduleInput,
  SchedulePreviewInput,
} from '@/lib/api/report-schedules';

/**
 * `lib/validation/reportSchedule.ts` — HO-RPT-002 §8.3, §13. SHAPE only: required fields, lengths
 * and the 5-minute send-time step. Every rule that depends on the report, the branch, the period
 * or a recipient is the server's, and arrives as a field error (NFR-USA-4). Nothing here computes a
 * recurrence (ADR-FE-136).
 */
export const reportScheduleSchema = z
  .object({
    name: z.string().trim().min(1, 'Enter a name.').max(100, 'Use 100 characters or fewer.'),
    reportKey: z.string().min(1, 'Choose a report.'),
    /** '' = all branches. */
    branchId: z.string(),
    period: z.string().min(1, 'Choose a period.'),
    format: z.enum(['Csv', 'Pdf']),
    frequency: z.enum(['Daily', 'Weekly', 'Monthly']),
    dayOfWeek: z.string(),
    monthlyMode: z.enum(['day', 'last']),
    dayOfMonth: z.string(),
    sendAtLocal: z
      .string()
      .regex(/^\d{2}:\d{2}$/, 'Give the send time as HH:mm.')
      .refine((value) => Number(value.slice(3)) % 5 === 0, 'Use a 5-minute step, such as 07:05.'),
    recipientIds: z.array(z.string()).min(1, 'Choose at least one recipient.'),
    parameters: z.record(z.string(), z.string()),
  })
  .superRefine((values, context) => {
    if (values.frequency === 'Weekly' && !values.dayOfWeek) {
      context.addIssue({
        code: 'custom',
        path: ['dayOfWeek'],
        message: 'Choose a day of the week.',
      });
    }
    if (values.frequency === 'Monthly' && values.monthlyMode === 'day' && !values.dayOfMonth) {
      context.addIssue({
        code: 'custom',
        path: ['dayOfMonth'],
        message: 'Choose a day of the month.',
      });
    }
  });

export type ReportScheduleFormValues = z.infer<typeof reportScheduleSchema>;

export const EMPTY_SCHEDULE_VALUES: ReportScheduleFormValues = {
  name: '',
  reportKey: '',
  branchId: '',
  period: '',
  format: 'Pdf',
  frequency: 'Daily',
  dayOfWeek: '',
  monthlyMode: 'day',
  dayOfMonth: '1',
  sendAtLocal: '07:00',
  recipientIds: [],
  parameters: {},
};

function frequencyFields(values: ReportScheduleFormValues) {
  const weekly = values.frequency === 'Weekly';
  const monthly = values.frequency === 'Monthly';
  return {
    frequency: values.frequency as ReportFrequency,
    dayOfWeek: weekly ? values.dayOfWeek || null : null,
    dayOfMonth: monthly && values.monthlyMode === 'day' ? Number(values.dayOfMonth) || null : null,
    lastDayOfMonth: monthly && values.monthlyMode === 'last',
    sendAtLocal: values.sendAtLocal,
  };
}

/** The request body for create and update. */
export function toScheduleInput(
  values: ReportScheduleFormValues,
  expectedVersion?: number,
): ReportScheduleInput {
  const parameters = Object.fromEntries(
    Object.entries(values.parameters).filter(([, value]) => value !== ''),
  );
  return {
    name: values.name.trim(),
    reportKey: values.reportKey,
    branchId: values.branchId === '' || values.branchId === 'all' ? null : values.branchId,
    period: values.period,
    parameters,
    format: values.format,
    recipientIds: values.recipientIds,
    ...frequencyFields(values),
    ...(expectedVersion !== undefined ? { expectedVersion } : {}),
  };
}

/** The body of the preview: the timing fields only; `null` while the draft is incomplete. */
export function toPreviewInput(values: ReportScheduleFormValues): SchedulePreviewInput | null {
  if (!values.reportKey || !values.period) return null;
  if (!/^\d{2}:\d{2}$/.test(values.sendAtLocal)) return null;
  if (values.frequency === 'Weekly' && !values.dayOfWeek) return null;
  if (values.frequency === 'Monthly' && values.monthlyMode === 'day' && !values.dayOfMonth) {
    return null;
  }
  return {
    reportKey: values.reportKey,
    branchId: values.branchId === '' || values.branchId === 'all' ? null : values.branchId,
    period: values.period,
    ...frequencyFields(values),
  };
}

/** Form values from a loaded schedule (edit). */
export function fromScheduleDetail(detail: ReportScheduleDetail): ReportScheduleFormValues {
  return {
    name: detail.name,
    reportKey: detail.reportKey,
    branchId: detail.branchId ?? '',
    period: detail.period,
    format: detail.format === 'Csv' ? 'Csv' : 'Pdf',
    frequency:
      (['Daily', 'Weekly', 'Monthly'] as const).find((f) => f === detail.frequency) ?? 'Daily',
    dayOfWeek: detail.dayOfWeek ?? '',
    monthlyMode: detail.lastDayOfMonth ? 'last' : 'day',
    dayOfMonth: detail.dayOfMonth ? String(detail.dayOfMonth) : '1',
    sendAtLocal: detail.sendAtLocal.slice(0, 5),
    recipientIds: detail.recipients.map((recipient) => recipient.id),
    parameters: { ...detail.parameters },
  };
}
