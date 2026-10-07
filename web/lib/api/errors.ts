import type { FieldValues, Path, UseFormSetError } from 'react-hook-form';
import { ApiConflictError, ApiForbiddenError, ApiValidationError } from './client';

const GENERIC_ERROR_MESSAGE = 'Something went wrong. Please try again.';

/**
 * Bridges the API's ValidationResponse to react-hook-form (HO-CAT-002 §11.8). This is where
 * NFR-USA-4 is met or failed:
 *  - `errorMessage` is written for a human by the API and is shown as-is.
 *  - `errorCode` (e.g. "MicrochipAlreadyRegistered") is only ever read to pick an area's OWN
 *    fallback sentence from a `codeMessages` map the caller passes in (used when `errorMessage`
 *    is empty); it is never rendered. With no map the fallback is the generic sentence — no
 *    area's wording is built in here.
 *  - An error whose field isn't one of `knownFields` becomes a form-level message under `root`
 *    instead of being silently dropped — a validation failure with no visible cause is worse
 *    than a crash.
 *
 * `fieldMap` translates the API command's `propertyName` to this form's field name where they
 * differ. Keep that mapping in one table per form, not scattered across the codebase.
 */
export function applyValidationErrors<TFieldValues extends FieldValues>(
  error: ApiValidationError,
  setError: UseFormSetError<TFieldValues>,
  options: {
    fieldMap?: Partial<Record<string, Path<TFieldValues>>>;
    knownFields?: ReadonlyArray<Path<TFieldValues>>;
    codeMessages?: Readonly<Record<string, string>>;
  } = {},
): void {
  const { fieldMap = {}, knownFields, codeMessages } = options;
  const rootField = 'root' as Path<TFieldValues>;

  if (error.response.errors.length === 0) {
    setError(rootField, { type: 'server', message: 'Something went wrong. Please try again.' });
    return;
  }

  for (const validationError of error.response.errors) {
    const mapped = fieldMap[validationError.propertyName];
    const target = mapped ?? (validationError.propertyName as Path<TFieldValues>);
    const isKnown = !knownFields || knownFields.includes(target);

    setError(isKnown ? target : rootField, {
      type: 'server',
      // The server's message is preferred; the caller's mapped plain-language text is the fallback
      // when it is empty. `errorCode` is read only to pick that fallback, never shown.
      message:
        validationError.errorMessage ||
        codeMessages?.[validationError.errorCode] ||
        GENERIC_ERROR_MESSAGE,
    });
  }
}

/**
 * A form's submit handler, wrapped so every Cat Profiles form applies §11.8 the same way instead
 * of each one re-writing its own try/catch (added for HO-CAT-002; the function above is
 * HO-CAT-002 §11.8's own spec, unchanged):
 *  - `ApiValidationError` → `applyValidationErrors` (field-level, or `root` when unmapped).
 *  - `ApiConflictError` (HO-CAT-001's `409`, a concurrent edit) → a `root` message asking the
 *    user to get the latest version and try again — component test E20's "user is told the
 *    record changed", **never a silent overwrite**. The caller's field values are untouched
 *    either way, since nothing here resets the form (NFR-USA-5).
 *  - anything else (network failure, `5xx`) → a plain-language `root` message, never the raw
 *    error (NFR-USA-4, E21).
 *
 * Returns the action's result on success, or `undefined` after recording the error — callers
 * that need to react to success (closing a dialog, navigating) check for that.
 */
export async function submitWithServerErrors<TFieldValues extends FieldValues, TResult>(
  action: () => Promise<TResult>,
  setError: UseFormSetError<TFieldValues>,
  options: {
    fieldMap?: Partial<Record<string, Path<TFieldValues>>>;
    knownFields?: ReadonlyArray<Path<TFieldValues>>;
    codeMessages?: Readonly<Record<string, string>>;
  } = {},
): Promise<TResult | undefined> {
  try {
    return await action();
  } catch (error) {
    const rootField = 'root' as Path<TFieldValues>;
    if (error instanceof ApiValidationError) {
      applyValidationErrors(error, setError, options);
    } else if (error instanceof ApiConflictError) {
      setError(rootField, {
        type: 'server',
        message:
          'Someone else changed this record while you were editing it. Get the latest version, then try saving again — your entered changes are still here.',
      });
    } else {
      setError(rootField, { type: 'server', message: describeUnexpectedError(error) });
    }
    return undefined;
  }
}

/**
 * `applyValidationErrors`/`submitWithServerErrors` both need a react-hook-form `setError` —
 * fine for every Cat Profiles form, but `PhotoField` (§11.6) isn't a react-hook-form form at all
 * (it's a plain controlled input plus a button click, not a `<form onSubmit>`). This is the same
 * §11.8 mapping for that case: every error shown (never just `errors[0]` — "must never be
 * swallowed" applies here too), a `409` gets its own message, and `errorCode` is only read to
 * pick a fallback from the caller's optional `codeMessages` (never rendered). Returns one display string rather than setting per-field state, since the caller
 * (`PhotoField`'s `serverError` prop) only has one error slot to show.
 */
export function describeApiError(
  error: unknown,
  codeMessages?: Readonly<Record<string, string>>,
): string {
  if (error instanceof ApiForbiddenError) return describeForbidden(error);
  if (error instanceof ApiValidationError) {
    if (error.response.errors.length === 0) {
      return 'Something went wrong. Please try again.';
    }
    return error.response.errors
      .map(
        (validationError) =>
          validationError.errorMessage ||
          codeMessages?.[validationError.errorCode] ||
          GENERIC_ERROR_MESSAGE,
      )
      .join(' ');
  }
  if (error instanceof ApiConflictError) {
    return 'Someone else changed this record while you were working. Reload and try again.';
  }
  return 'Something went wrong. Please try again.';
}

/**
 * HO-ADM-002 §18 A3 — a server `403` on a save/action is shown as a clear permission message, never
 * the generic "Something went wrong. Please try again." (retrying a refusal cannot help). Names no
 * role and no action, like `PermissionDeniedState` (ADR-FE-009); the two branch-assignment codes
 * (§11.9) get their own wording because they mean "pick or get assigned a branch", not "never".
 */
export const PERMISSION_DENIED_MESSAGE =
  "You don't have permission to do this. Ask an administrator if you need access.";

function describeForbidden(error: ApiForbiddenError): string {
  const code = error.body.errors?.[0]?.errorCode;
  if (code === 'BranchNotAssigned') {
    return "You aren't assigned to the active branch. Choose one of your branches from the branch picker.";
  }
  if (code === 'BranchAssignmentRequired') {
    return 'Ask an administrator to assign you to a branch.';
  }
  return PERMISSION_DENIED_MESSAGE;
}

/** The root message for a submit that failed with anything other than a `400`: a permission
 *  message for a `403`, otherwise the generic plain-language retry text (NFR-USA-4). */
export function describeUnexpectedError(error: unknown): string {
  if (error instanceof ApiForbiddenError) return describeForbidden(error);
  return 'Something went wrong. Please try again.';
}

/**
 * HO-ADM-002 §11.9 — `BranchNotAssigned` (a stale/refused `X-Branch-Id`) and
 * `BranchAssignmentRequired` (no assignment, and more than one active branch exists) are the two
 * `403` codes every branch-scoped admin page can hit even for a role that otherwise holds the
 * action — they mean "pick or get assigned a branch", not "you may never do this". Callers show a
 * banner with a link to the branch picker (`BranchNotAssigned`) or the plain-language ask-an-
 * administrator text (`BranchAssignmentRequired`) instead of falling through to the generic
 * `PermissionDeniedState`, which would wrongly suggest the action itself is forbidden.
 */
export type BranchAssignmentErrorCode = 'BranchNotAssigned' | 'BranchAssignmentRequired';

export function getBranchAssignmentErrorCode(error: unknown): BranchAssignmentErrorCode | null {
  if (!(error instanceof ApiForbiddenError)) return null;
  const code = error.body.errors?.[0]?.errorCode;
  return code === 'BranchNotAssigned' || code === 'BranchAssignmentRequired' ? code : null;
}

// -------------------------------------------------------------------------------------------
// HO-NTF-002 §11.6 — Notifications. `errorCode` is read here only to CHOOSE a plain-language
// sentence; it is never rendered (NFR-USA-4).
// -------------------------------------------------------------------------------------------

export type TemplateContentField = 'subject' | 'title' | 'body' | 'linkPath';

/**
 * C1 / preview violations name their field as `Contents[i].Subject | Title | Body | LinkPath`
 * (HO-NTF-001 §8.2 rule 3). `i` indexes the `contents` array the client SENT, so the caller maps
 * the index back to a channel tab. `Contents` on its own (no index) has no field and returns null.
 */
export function parseTemplateContentsProperty(
  propertyName: string,
): { index: number; field: TemplateContentField } | null {
  const match = /^Contents\[(\d+)\]\.(Subject|Title|Body|LinkPath)$/i.exec(propertyName);
  if (!match) return null;
  const field = match[2]!;
  return {
    index: Number(match[1]),
    field: (field.charAt(0).toLowerCase() + field.slice(1)) as TemplateContentField,
  };
}

/** C8 (preferences) `PreferredChannel`, C7 (resend) `Channel` -> the form field of the same meaning. */
export const PREFERRED_CHANNEL_FIELD_MAP = { PreferredChannel: 'preferredChannel' } as const;
export const RESEND_FIELD_MAP = { Channel: 'channel' } as const;

export const TEMPLATE_CONFLICT_MESSAGE =
  'Someone else changed this template. Reload to see their version.';

function firstErrorCode(error: ApiValidationError): string | undefined {
  return error.response?.errors?.[0]?.errorCode;
}

/** A resend that the server refused (C7), as one plain sentence. `null` when it is not one of the
 *  three resend-specific refusals, so the caller falls back to `describeApiError`. */
export function describeResendRefusal(error: unknown): string | null {
  if (!(error instanceof ApiValidationError)) return null;
  const code = firstErrorCode(error);
  if (code === 'ResendLimitReached') return 'This message has already been resent 3 times.';
  if (code === 'ResendChannelNotAvailable') {
    return "That channel isn't available for this message. Choose another one.";
  }
  if (code === 'NotificationCannotBeResent') {
    // The API writes this sentence for a person (it names the reason) — shown as-is.
    return error.response.errors[0]?.errorMessage ?? "This message can't be resent right now.";
  }
  return null;
}

export const INVALID_UNSUBSCRIBE_LINK_MESSAGE =
  "This link isn't valid any more. You can manage your preferences after signing in.";
export const UNSUBSCRIBE_RATE_LIMITED_MESSAGE =
  'Too many attempts. Please wait a minute and try again.';

export function isInvalidUnsubscribeLink(error: unknown): boolean {
  return error instanceof ApiValidationError && firstErrorCode(error) === 'InvalidUnsubscribeLink';
}

// ---------------------------------------------------------------------------------------------
// HO-APT-002 §11.8 — Appointments. Command `propertyName`s (PascalCase) -> form field names, and
// the HO-APT-001 §10.4 violation codes -> the plain-language text used in §11. The SERVER'S
// message is preferred when present (it names the conflicting time, e.g. "Dr Rahman already has
// an appointment 11:30–12:00."); the text below is the fallback. `errorCode` itself is read here
// only to pick that fallback and to branch behaviour — it is never rendered (NFR-USA-4).
// ---------------------------------------------------------------------------------------------

export const APPOINTMENT_FIELD_MAP = {
  BranchId: 'branchId',
  ServiceId: 'serviceId',
  CatId: 'catId',
  CustomerId: 'customerId',
  StartsOn: 'startsOn',
  PerformerId: 'performerId',
  RoomId: 'roomId',
  ReasonForVisit: 'reasonForVisit',
  Reason: 'reason',
  TriagePriority: 'triagePriority',
  PresentingComplaint: 'presentingComplaint',
  LocalDate: 'localDate',
  PreferredPerformerId: 'preferredPerformerId',
  ExpectedVersion: 'expectedVersion',
} as const;

export const APPOINTMENT_VIOLATION_MESSAGES: Record<string, string> = {
  SlotNoLongerAvailable: 'Someone has just taken that time. Please choose another.',
  SlotHoldExpired: 'Your hold has ended — please choose a time again.',
  ChangeCutoffPassed:
    "It's too close to the appointment to change it online. Please call the clinic.",
  PerformerBusy: 'This vet already has an appointment at that time.',
  RoomBusy: 'That room is already in use at that time.',
  CatAlreadyBooked: 'This cat already has an appointment at that time.',
  PerformerNotAvailable: 'This vet is not available at that time.',
  PerformerNotQualified: 'This vet is not qualified for this service.',
  RoomKindMismatch: 'That room is not the right kind of room for this service.',
  InvalidAppointmentTransition: 'That is not possible for an appointment in its current state.',
  NoShowTooEarly:
    'A no-show can only be recorded once the grace period after the start has passed.',
  CheckInNotToday: 'This appointment can only be checked in on the day it is booked for.',
  SlotsStillAvailable: 'Times have just become available — pick one below.',
  AlreadyWaitlisted: 'This cat is already on the waitlist for that day.',
  CatNotBookable: 'This cat cannot be booked right now.',
  CancellationReasonRequired: 'Enter a reason for cancelling.',
  ServiceNotBookable: 'This service cannot be booked online.',
  SlotOutsideAvailability: 'That time is outside the vet’s available hours.',
  SlotTooSoon: 'That time is too soon to book online. Please choose a later time.',
  SlotBeyondHorizon: 'That date is too far ahead to book yet.',
  HoldNotFound: 'This hold could not be found. Please choose a time again.',
  AppointmentNotFound: 'This appointment could not be found.',
};

/** Codes whose fixed wording is part of the screen's own flow (US-CLN-03 AC2/AC3), so the mapped
 *  text wins over whatever the server's generic message says. */
const ALWAYS_MAPPED_CODES = new Set(['SlotNoLongerAvailable', 'SlotHoldExpired']);

/** The `errorCode` of a `400`/`409`'s first violation, for BEHAVIOUR only (never rendered). */
export function getViolationCode(error: unknown): string | null {
  if (error instanceof ApiValidationError) return error.response.errors[0]?.errorCode ?? null;
  if (error instanceof ApiConflictError) return error.response?.errors[0]?.errorCode ?? null;
  return null;
}

/** One sentence for an Appointments failure: the mapped text for the always-mapped codes, else the
 *  server's own message, else the mapped fallback, else the generic text; a `403` reads as a
 *  permission message. */
export function describeAppointmentError(error: unknown): string {
  if (error instanceof ApiForbiddenError) return describeForbidden(error);
  const code = getViolationCode(error);
  if (code && ALWAYS_MAPPED_CODES.has(code)) return APPOINTMENT_VIOLATION_MESSAGES[code] ?? '';
  const response =
    error instanceof ApiValidationError
      ? error.response
      : error instanceof ApiConflictError
        ? error.response
        : undefined;
  const serverMessage = response?.errors
    .map((violation) => violation.errorMessage)
    .filter(Boolean)
    .join(' ');
  if (serverMessage) return serverMessage;
  if (code && APPOINTMENT_VIOLATION_MESSAGES[code]) return APPOINTMENT_VIOLATION_MESSAGES[code]!;
  if (error instanceof ApiConflictError) {
    return 'This appointment changed since you loaded it. The latest version has been loaded.';
  }
  return 'Something went wrong. Please try again.';
}

/**
 * HO-SHP-002 §11.7 — HO-SHP-001 §10.4 codes mapped to the plain-language messages used in the shop
 * screens. The server's own message is always preferred when present; this is the fallback
 * (NFR-USA-4: the code itself is never rendered, only the text it selects).
 */
export const SHOP_CODE_MESSAGES: Readonly<Record<string, string>> = {
  ProductNotPublishable: "This product can't be published yet. Fix the items marked below.",
  SlugAlreadyInUse: 'This web address is already used by another product.',
  SlugLockedAfterPublication: "The web address can't change once a product has been published.",
  SkuCodeAlreadyInUse: 'This SKU code is already in use.',
  BarcodeAlreadyInUse: 'This barcode is already used by another SKU.',
  InvalidBarcode: 'This barcode is not valid.',
  DuplicateVariant: 'A SKU with the same size, flavour and colour already exists.',
  VariantAttributesRequired: 'Add at least a size or weight, a flavour or a colour.',
  ImageAltTextRequired: 'Describe every image in a short alt text.',
  CountryOfOriginRequired: 'Add the country of origin before publishing this food product.',
  InvalidGuaranteedAnalysis: 'Enter all four analysis values, each between 0 and 100 %.',
  PriceCannotBeBackdated: "A new price can't start in the past.",
  PriceMustBePositive: 'The price must be more than zero.',
  InvalidBundleComponent:
    "This item can't be part of a kit. Kits can't contain other kits or prescription diets.",
  ReviewNotPermitted: 'Only customers who bought this product can review it.',
  AlreadyReviewed: 'You have already reviewed this product.',
  SkuAlreadyAvailable: 'This size is in stock again — you can buy it now.',
  StockAlertAlreadyRequested: 'You already have a pending alert for this item.',
  PurchaseNotPermitted: "This item can't be bought, so we can't set an alert for it.",
  LocaleNotEnabled: 'That language is not enabled for the shop.',
  InvalidCatalogueFilter: 'One of the filters is not valid. Clear the filters and try again.',
};

/** Every HO-SHP-001 command `propertyName` (PascalCase on the wire). `SHOP_FIELD_MAP` maps each to
 *  the lower-camel form field of the same name, so a section form passes one shared table. */
export const SHOP_PROPERTY_NAMES = [
  'Name',
  'Brand',
  'Description',
  'Category',
  'Subcategory',
  'Tags',
  'TaxClassId',
  'CountryOfOriginCode',
  'ListedOnStorefront',
  'IsExpiryTracked',
  'HealthFocus',
  'Slug',
  'Kind',
  'BundleSkuCode',
  'Code',
  'Barcode',
  'SizeOrWeight',
  'Flavour',
  'Colour',
  'PackQuantity',
  'Manufacturer',
  'LifeStage',
  'ProteinSource',
  'IngredientList',
  'GuaranteedAnalysis',
  'ProteinPercent',
  'FatPercent',
  'FibrePercent',
  'MoisturePercent',
  'FeedingGuide',
  'StorageInstructions',
  'PrescriptionRequired',
  'Images',
  'FileId',
  'AltText',
  'Components',
  'ComponentSkuId',
  'Quantity',
  'Links',
  'RelatedProductId',
  'Locale',
  'Amount',
  'CurrencyCode',
  'EffectiveFrom',
  'CountryCode',
  'SourcingNotes',
  'IsFeatured',
  'SortOrder',
  'Rating',
  'Title',
  'Body',
  'SkuId',
  'ProductId',
  'Reason',
  'Note',
] as const;

export const SHOP_FIELD_MAP: Readonly<Record<string, string>> = Object.fromEntries(
  SHOP_PROPERTY_NAMES.map((name) => [name, name.charAt(0).toLowerCase() + name.slice(1)]),
);
