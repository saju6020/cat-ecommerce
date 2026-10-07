import { z } from 'zod';

/**
 * `lib/validation/appointments.ts` — HO-APT-002 zod schemas. They mirror the HO-APT-001 command
 * validators (the server stays the authority — NFR-USA-4): reason for visit and presenting
 * complaint are plain text up to 500 characters; a cancellation reason is required for staff.
 */
export const MAX_APPOINTMENT_TEXT = 500;

export const reasonForVisitSchema = z.object({
  reasonForVisit: z
    .string()
    .max(MAX_APPOINTMENT_TEXT, `Keep this to ${MAX_APPOINTMENT_TEXT} characters or fewer.`),
});
export type ReasonForVisitValues = z.infer<typeof reasonForVisitSchema>;

export const staffCancelSchema = z.object({
  reason: z
    .string()
    .trim()
    .min(1, 'Enter a reason for cancelling.')
    .max(MAX_APPOINTMENT_TEXT, `Keep this to ${MAX_APPOINTMENT_TEXT} characters or fewer.`),
});
export type StaffCancelValues = z.infer<typeof staffCancelSchema>;

export const customerCancelSchema = z.object({
  reason: z
    .string()
    .max(MAX_APPOINTMENT_TEXT, `Keep this to ${MAX_APPOINTMENT_TEXT} characters or fewer.`),
});
export type CustomerCancelValues = z.infer<typeof customerCancelSchema>;

export const walkInSchema = z.object({
  customerId: z.string().min(1, 'Find or add the customer first.'),
  catId: z.string().min(1, 'Choose a cat.'),
  serviceId: z.string().min(1, 'Choose a service.'),
  triagePriority: z.enum(['Routine', 'Urgent', 'Emergency']),
  presentingComplaint: z
    .string()
    .max(MAX_APPOINTMENT_TEXT, `Keep this to ${MAX_APPOINTMENT_TEXT} characters or fewer.`),
});
export type WalkInValues = z.infer<typeof walkInSchema>;

export const staffBookingSchema = z.object({
  customerId: z.string().min(1, 'Find the customer first.'),
  catId: z.string().min(1, 'Choose a cat.'),
  serviceId: z.string().min(1, 'Choose a service.'),
  date: z.string().min(1, 'Choose a date.'),
  performerId: z.string().min(1, 'Choose a vet or nurse.'),
  slotStart: z.string().min(1, 'Choose a time.'),
  reasonForVisit: z
    .string()
    .max(MAX_APPOINTMENT_TEXT, `Keep this to ${MAX_APPOINTMENT_TEXT} characters or fewer.`),
});
export type StaffBookingValues = z.infer<typeof staffBookingSchema>;

export const moveSchema = z.object({
  date: z.string().min(1, 'Choose a date.'),
  performerId: z.string().min(1, 'Choose a vet or nurse.'),
  roomId: z.string().min(1, 'Choose a room.'),
  time: z.string().regex(/^\d{2}:\d{2}$/, 'Enter a time, for example 11:30.'),
});
export type MoveValues = z.infer<typeof moveSchema>;

export const startSchema = z.object({
  performerId: z.string().min(1, 'Choose who will see this cat.'),
  roomId: z.string().min(1, 'Choose a room.'),
});
export type StartValues = z.infer<typeof startSchema>;
