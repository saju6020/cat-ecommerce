import { z } from 'zod';
import { MONEY_FORMAT_MESSAGE, MONEY_PATTERN } from '@/lib/money';
import { guardedText } from './schemas';

/**
 * The Record-payment form's values and zod schema (HO-BIL-002 §11.4). Amounts are strings validated
 * by `MONEY_PATTERN` (never parsed). `externalReference` and `notes` run the C-5 card-number guard —
 * on change (the form uses `mode: 'onChange'`) and on submit — so a Luhn-valid 13–19-digit run is a
 * field error, nothing is sent, and the message never repeats it (ADR-FE-084). Method-dependent
 * requirements (reference, collector) are checked in the submit handler against the server's flags.
 */
export const paymentSchema = z.object({
  method: z.string().min(1, 'Choose a payment method.'),
  amount: z.string().trim().min(1, 'Enter the amount.').regex(MONEY_PATTERN, MONEY_FORMAT_MESSAGE),
  receivedOn: z.string().regex(/^\d{4}-\d{2}-\d{2}$/, 'Enter the date received.'),
  externalReference: guardedText(100),
  collectorKind: z.enum(['Courier', 'Staff']),
  courierCode: guardedText(30),
  courierName: guardedText(100),
  notes: guardedText(500),
});

export type PaymentFormValues = z.infer<typeof paymentSchema>;

export const PAYMENT_KNOWN_FIELDS = [
  'method',
  'amount',
  'receivedOn',
  'externalReference',
  'collectorKind',
  'courierCode',
  'courierName',
  'notes',
] as const;
